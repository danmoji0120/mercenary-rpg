"""Deterministic transparent-character pixel conversion core.

Based on tsutsuji815/pixel_convert (https://github.com/tsutsuji815/pixel_convert),
BSD 2-Clause. Modified for mercenary-rpg batch asset processing.

The upstream resize -> nearest upscale -> K-means idea is retained, while
transparent pixels are excluded from clustering and alpha is kept as binary.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import time
from pathlib import Path
from typing import Any

import cv2
import numpy as np
from PIL import Image


ALGORITHM_VERSION = "mercenary-pixel-convert-1.0.0"
MAX_RECOMMENDED_SOURCE_PIXELS = 64_000_000
SATURATION_LEVELS = {"none": 1.0, "low": 1.10, "medium": 1.20, "high": 1.35}

cv2.setNumThreads(1)


class ConversionValidationError(RuntimeError):
    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code


def sha256_file(path: Path, chunk_size: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(chunk_size):
            digest.update(chunk)
    return digest.hexdigest()


def stable_json_hash(value: Any) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def load_preset_document(path: Path) -> dict[str, Any]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(document.get("presets"), dict):
        raise ValueError("presets.json must contain a presets object.")
    return document


def resolve_preset(
    document: dict[str, Any],
    preset_name: str,
    saturation: str | None = None,
    saturation_multiplier: float | None = None,
) -> dict[str, Any]:
    if preset_name not in document["presets"]:
        raise KeyError(f"Unknown preset: {preset_name}")
    preset = dict(document["presets"][preset_name])
    levels = dict(SATURATION_LEVELS)
    levels.update(document.get("saturation_levels", {}))
    level = saturation if saturation is not None else preset.get("increase_saturation", "none")
    if level not in levels:
        raise ValueError(f"Unknown saturation level: {level}")
    multiplier = saturation_multiplier
    if multiplier is None:
        multiplier = preset.get("saturation_multiplier", levels[level])
    if multiplier <= 0:
        raise ValueError("saturation_multiplier must be greater than zero.")
    preset["increase_saturation"] = level
    preset["saturation_multiplier"] = float(multiplier)
    _validate_preset(preset)
    return preset


def _validate_preset(preset: dict[str, Any]) -> None:
    preserve_source_dimensions = bool(preset.get("preserve_source_dimensions", False))
    raw_target_height = preset.get("target_content_height")
    if preserve_source_dimensions:
        # A preserved source has no target height.  Normalize the value so
        # hashes and all callers use the same stable representation.
        preset["target_content_height"] = None
    else:
        if raw_target_height is None:
            raise ValueError(
                "target_content_height is required when preserve_source_dimensions is false"
            )
        try:
            target_content_height = int(raw_target_height)
        except (TypeError, ValueError) as exc:
            raise ValueError(
                "target_content_height must be a positive integer when preserve_source_dimensions is false"
            ) from exc
        if target_content_height <= 0:
            raise ValueError("target_content_height must be greater than 0")
        preset["target_content_height"] = target_content_height

    for key in ("pixel_block_size", "palette_colors"):
        if int(preset.get(key, 0)) < 1:
            raise ValueError(f"{key} must be at least 1.")
    if int(preset.get("padding", 0)) < 0:
        raise ValueError("padding must not be negative.")
    threshold = int(preset.get("alpha_threshold", 16))
    if not 0 <= threshold <= 254:
        raise ValueError("alpha_threshold must be between 0 and 254.")


def _source_has_alpha(image: Image.Image) -> bool:
    return "A" in image.getbands() or (image.mode == "P" and "transparency" in image.info)


def _mask_bbox(mask: np.ndarray) -> tuple[int, int, int, int] | None:
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def _ceil_multiple(value: int, multiple: int) -> int:
    return max(multiple, ((value + multiple - 1) // multiple) * multiple)


def _estimate_unique_colors(rgb: np.ndarray, mask: np.ndarray, limit: int = 100_000) -> int:
    pixels = rgb[mask]
    if len(pixels) == 0:
        return 0
    if len(pixels) > limit:
        stride = max(1, len(pixels) // limit)
        pixels = pixels[::stride][:limit]
    return int(len(np.unique(pixels, axis=0)))


def _adjust_saturation(
    rgb: np.ndarray,
    alpha: np.ndarray,
    alpha_threshold: int,
    multiplier: float,
) -> tuple[np.ndarray, int, int]:
    if abs(multiplier - 1.0) < 1e-9:
        return rgb, 0, 0
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    mask = alpha > alpha_threshold
    saturation = hsv[:, :, 1].astype(np.float32)
    selected = saturation[mask]
    if selected.size == 0:
        return rgb, 0, 0
    scaled = selected * multiplier
    clamped_count = int(np.count_nonzero(scaled > 255.0))
    adjusted = np.clip(np.rint(scaled), 0, 255).astype(np.uint8)
    changed_count = int(np.count_nonzero(adjusted != selected.astype(np.uint8)))
    saturation[mask] = adjusted
    hsv[:, :, 1] = saturation.astype(np.uint8)
    return cv2.cvtColor(hsv, cv2.COLOR_HSV2RGB), changed_count, clamped_count


def _deterministic_seed(source_sha256: str, preset_name: str, preset_hash: str) -> int:
    material = f"{source_sha256}|{preset_name}|{preset_hash}|{ALGORITHM_VERSION}".encode("utf-8")
    value = int.from_bytes(hashlib.sha256(material).digest()[:4], "big") & 0x7FFFFFFF
    return value or 1


def _quantize_opaque(
    rgb: np.ndarray,
    opaque_mask: np.ndarray,
    requested_colors: int,
    seed: int,
) -> tuple[np.ndarray, int]:
    pixels = rgb[opaque_mask]
    if len(pixels) == 0:
        raise ConversionValidationError("EMPTY_ALPHA", "No opaque pixels remained after logical downsampling.")
    unique = np.unique(pixels, axis=0)
    actual_k = min(int(requested_colors), len(unique), len(pixels))
    output = np.zeros_like(rgb, dtype=np.uint8)
    if actual_k == 1:
        output[opaque_mask] = unique[0]
        return output, 1

    cv2.setRNGSeed(int(seed))
    samples = pixels.astype(np.float32)
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 40, 0.2)
    _, labels, centers = cv2.kmeans(
        samples,
        actual_k,
        None,
        criteria,
        1,
        cv2.KMEANS_PP_CENTERS,
    )
    centers = np.clip(np.rint(centers), 0, 255).astype(np.uint8)
    output[opaque_mask] = centers[labels.reshape(-1)]
    return output, int(actual_k)


def _save_png_atomic(image: Image.Image, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = output_path.with_name(f".{output_path.name}.{os.getpid()}.tmp.png")
    image.save(temp_path, format="PNG", compress_level=9, optimize=False)
    temp_path.replace(output_path)


def convert_character(
    source_path: str | Path,
    output_path: str | Path,
    preset_name: str,
    preset: dict[str, Any],
    source_relative_path: str | None = None,
) -> dict[str, Any]:
    started = time.perf_counter()
    source = Path(source_path).resolve()
    output = Path(output_path).resolve()
    if source == output:
        raise ConversionValidationError("SOURCE_OVERWRITE_BLOCKED", "Output path must not overwrite the source PNG.")
    if source.suffix.lower() != ".png":
        raise ConversionValidationError("NOT_PNG", "Only PNG input files are supported.")

    _validate_preset(preset)
    preserve_source_dimensions = bool(preset.get("preserve_source_dimensions", False))
    target_content_height: int | None = preset.get("target_content_height")
    source_sha = sha256_file(source)
    preset_hash = stable_json_hash({"name": preset_name, "config": preset})
    warnings: list[str] = []

    try:
        with Image.open(source) as opened:
            source_had_alpha = _source_has_alpha(opened)
            source_size = tuple(int(v) for v in opened.size)
            if source_size[0] <= 0 or source_size[1] <= 0:
                raise ConversionValidationError("INVALID_SIZE", "Source width and height must be positive.")
            if source_size[0] * source_size[1] > MAX_RECOMMENDED_SOURCE_PIXELS:
                warnings.append("SOURCE_TOO_LARGE")
            if not source_had_alpha:
                warnings.append("SOURCE_HAS_NO_ALPHA")
            rgba_image = opened.convert("RGBA")
    except ConversionValidationError:
        raise
    except Exception as exc:
        raise ConversionValidationError("SOURCE_OPEN_FAILED", f"Unable to open source PNG: {exc}") from exc

    rgba = np.asarray(rgba_image, dtype=np.uint8).copy()
    alpha_threshold = int(preset.get("alpha_threshold", 16))
    source_mask = rgba[:, :, 3] > alpha_threshold
    source_bbox = _mask_bbox(source_mask)
    if source_bbox is None:
        raise ConversionValidationError("EMPTY_ALPHA", "Source contains no pixels above alpha_threshold.")
    if source_bbox[0] == 0 or source_bbox[1] == 0 or source_bbox[2] == source_size[0] or source_bbox[3] == source_size[1]:
        warnings.append("BOUNDING_BOX_TOUCHES_EDGE")

    x0, y0, x1, y1 = source_bbox
    crop = rgba[y0:y1, x0:x1].copy()
    crop[crop[:, :, 3] <= alpha_threshold, :3] = 0
    crop_height, crop_width = crop.shape[:2]
    if preserve_source_dimensions:
        ratio = 1.0
        target_height = crop_height
        target_width = crop_width
        target_image = Image.fromarray(crop, mode="RGBA")
    else:
        # _validate_preset guarantees a positive integer on this branch.
        assert target_content_height is not None
        ratio = target_content_height / crop_height
        target_height = target_content_height
        target_width = max(1, int(round(crop_width * ratio)))
        target_image = Image.fromarray(crop, mode="RGBA").resize(
            (target_width, target_height), Image.Resampling.LANCZOS
        )
    aspect = crop_width / crop_height
    if aspect > 2.5:
        warnings.append("CONTENT_TOO_WIDE")
    if aspect < 0.18:
        warnings.append("CONTENT_TOO_THIN")

    block = int(preset["pixel_block_size"])
    if preserve_source_dimensions:
        # Preserve the cropped content extent, but still apply block-size
        # pixelization through an aligned working canvas and logical grid.
        target = np.asarray(target_image, dtype=np.uint8).copy()
        target_alpha = target[:, :, 3]
        target_rgb = target[:, :, :3]

        erode = int(preset.get("erode", 0))
        if erode:
            kernel = (
                np.ones((3, 3), np.uint8)
                if erode == 2
                else np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]], dtype=np.uint8)
            )
            target_alpha = cv2.erode(target_alpha, kernel, iterations=1)
        blur = int(preset.get("blur", 0))
        if blur:
            target_rgb = cv2.bilateralFilter(target_rgb, 5, float(blur), 20.0)

        aligned_width = _ceil_multiple(crop_width, block)
        aligned_height = _ceil_multiple(crop_height, block)
        aligned = np.zeros((aligned_height, aligned_width, 4), dtype=np.uint8)
        aligned[:crop_height, :crop_width, :3] = target_rgb
        aligned[:crop_height, :crop_width, 3] = target_alpha
        logical_width = max(1, aligned_width // block)
        logical_height = max(1, aligned_height // block)
        logical_source = np.asarray(
            Image.fromarray(aligned, mode="RGBA").resize(
                (logical_width, logical_height), Image.Resampling.NEAREST
            ),
            dtype=np.uint8,
        ).copy()
        logical_rgb = logical_source[:, :, :3]
        logical_alpha = logical_source[:, :, 3]

        saturation_multiplier = float(preset.get("saturation_multiplier", 1.0))
        logical_rgb, saturation_adjusted, saturation_clamped = _adjust_saturation(
            logical_rgb, logical_alpha, alpha_threshold, saturation_multiplier
        )
        logical_rgb[logical_alpha <= alpha_threshold] = 0
        source_unique_estimate = _estimate_unique_colors(target_rgb, target_alpha > alpha_threshold)
    else:
        # Keep the existing target-height path unchanged for world presets.
        target = np.asarray(target_image, dtype=np.uint8).copy()
        target_alpha = target[:, :, 3]
        target_rgb = target[:, :, :3]

        erode = int(preset.get("erode", 0))
        if erode:
            kernel = (
                np.ones((3, 3), np.uint8)
                if erode == 2
                else np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]], dtype=np.uint8)
            )
            target_alpha = cv2.erode(target_alpha, kernel, iterations=1)
        blur = int(preset.get("blur", 0))
        if blur:
            target_rgb = cv2.bilateralFilter(target_rgb, 5, float(blur), 20.0)

        saturation_multiplier = float(preset.get("saturation_multiplier", 1.0))
        target_rgb, saturation_adjusted, saturation_clamped = _adjust_saturation(
            target_rgb, target_alpha, alpha_threshold, saturation_multiplier
        )
        target_rgb[target_alpha <= alpha_threshold] = 0

        logical_width = max(1, math.ceil(target_width / block))
        logical_height = max(1, math.ceil(target_height / block))
        logical_rgb = np.asarray(
            Image.fromarray(target_rgb, mode="RGB").resize(
                (logical_width, logical_height), Image.Resampling.BOX
            ),
            dtype=np.uint8,
        )
        logical_alpha = np.asarray(
            Image.fromarray(target_alpha, mode="L").resize(
                (logical_width, logical_height), Image.Resampling.BOX
            ),
            dtype=np.uint8,
        )
        source_unique_estimate = _estimate_unique_colors(target_rgb, target_alpha > alpha_threshold)
    opaque_mask = logical_alpha > alpha_threshold
    if not np.any(opaque_mask):
        raise ConversionValidationError("EMPTY_ALPHA", "No pixels survived alpha quantization.")

    seed = _deterministic_seed(source_sha, preset_name, preset_hash)
    quantized_rgb, k = _quantize_opaque(
        logical_rgb, opaque_mask, int(preset["palette_colors"]), seed
    )
    if k < int(preset["palette_colors"]):
        warnings.append("PALETTE_REDUCED")
    logical_rgba = np.dstack((quantized_rgb, np.where(opaque_mask, 255, 0).astype(np.uint8)))
    logical_image = Image.fromarray(logical_rgba, mode="RGBA")
    if preserve_source_dimensions:
        pixelized = logical_image.resize((aligned_width, aligned_height), Image.Resampling.NEAREST)
        pixel_content = pixelized.crop((0, 0, crop_width, crop_height))
        content_width = crop_width
        content_height = crop_height
    else:
        content_width = logical_width * block
        content_height = logical_height * block
        pixel_content = logical_image.resize((content_width, content_height), Image.Resampling.NEAREST)

    padding = int(preset.get("padding", 0))
    canvas_width = _ceil_multiple(content_width + padding * 2, block)
    canvas_height = _ceil_multiple(content_height + padding * 2, block)
    canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))
    paste_x = (canvas_width - content_width) // 2
    paste_y = canvas_height - padding - content_height
    canvas.alpha_composite(pixel_content, (paste_x, paste_y))
    canvas_array = np.asarray(canvas, dtype=np.uint8).copy()
    canvas_array[:, :, :3][canvas_array[:, :, 3] == 0] = 0
    canvas_array[:, :, 3] = np.where(canvas_array[:, :, 3] > 0, 255, 0).astype(np.uint8)
    canvas = Image.fromarray(canvas_array, mode="RGBA")

    output_mask = canvas_array[:, :, 3] == 255
    output_bbox = _mask_bbox(output_mask)
    if output_bbox is None:
        raise ConversionValidationError("EMPTY_ALPHA", "Output unexpectedly contains no opaque pixels.")
    output_colors = np.unique(canvas_array[:, :, :3][output_mask], axis=0)
    alpha_values = sorted(int(v) for v in np.unique(canvas_array[:, :, 3]))
    if any(value not in (0, 255) for value in alpha_values):
        warnings.append("OUTPUT_HAS_PARTIAL_ALPHA")

    _save_png_atomic(canvas, output)
    output_sha = sha256_file(output)
    pivot_x = canvas_width / 2.0
    pivot_y = float(output_bbox[3])
    elapsed_ms = (time.perf_counter() - started) * 1000.0
    record: dict[str, Any] = {
        "sourcePath": str(source),
        "sourceRelativePath": source_relative_path or source.name,
        "sourceSha256": source_sha,
        "sourceHadNoAlpha": not source_had_alpha,
        "preset": preset_name,
        "presetConfigHash": preset_hash,
        "algorithmVersion": ALGORITHM_VERSION,
        "status": "WARNING" if warnings else "PASS",
        "skippedReason": "",
        "outputPath": str(output),
        "outputSha256": output_sha,
        "sourceSize": list(source_size),
        "sourceBoundingBox": list(source_bbox),
        "preserveSourceDimensions": preserve_source_dimensions,
        "targetContentHeight": target_content_height,
        "scaleRatio": ratio,
        "contentSize": [content_width, content_height],
        "outputSize": [canvas_width, canvas_height],
        "outputBoundingBox": list(output_bbox),
        "contentBoundingBox": list(output_bbox),
        "bottomCenterAnchor": [pivot_x, pivot_y],
        "normalizedPivotX": pivot_x / canvas_width,
        "normalizedPivotY": pivot_y / canvas_height,
        "requestedColors": int(preset["palette_colors"]),
        "actualColors": int(len(output_colors)),
        "requestedPaletteColorCount": int(preset["palette_colors"]),
        "actualPaletteColorCount": int(k),
        "opaquePixelCount": int(np.count_nonzero(output_mask)),
        "transparentPixelCount": int(output_mask.size - np.count_nonzero(output_mask)),
        "sourceUniqueColorEstimate": source_unique_estimate,
        "outputUniqueColorCount": int(len(output_colors)),
        "alphaValues": alpha_values,
        "saturationPreset": str(preset.get("increase_saturation", "none")),
        "saturationMultiplier": saturation_multiplier,
        "saturationAdjustedPixelCount": saturation_adjusted,
        "saturationClampedPixelCount": saturation_clamped,
        "elapsedMilliseconds": round(elapsed_ms, 3),
        "warningCodes": warnings,
        "errorMessage": "",
    }
    sidecar = output.with_suffix(output.suffix + ".json")
    sidecar.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    return record


def make_failed_record(
    source_path: str | Path,
    source_relative_path: str,
    preset_name: str,
    error: Exception,
    preset: dict[str, Any] | None = None,
) -> dict[str, Any]:
    source = Path(source_path)
    code = error.code if isinstance(error, ConversionValidationError) else "UNEXPECTED_ERROR"
    preserve_source_dimensions = (
        bool(preset.get("preserve_source_dimensions", False)) if preset is not None else None
    )
    target_content_height = preset.get("target_content_height") if preset is not None else None
    return {
        "sourcePath": str(source.resolve()),
        "sourceRelativePath": source_relative_path,
        "sourceSha256": sha256_file(source) if source.exists() else "",
        "preset": preset_name,
        "preserveSourceDimensions": preserve_source_dimensions,
        "targetContentHeight": target_content_height,
        "status": "FAILED",
        "skippedReason": "",
        "outputPath": "",
        "outputSha256": "",
        "sourceSize": [],
        "sourceBoundingBox": [],
        "contentSize": [],
        "outputSize": [],
        "outputBoundingBox": [],
        "bottomCenterAnchor": [],
        "requestedColors": 0,
        "actualColors": 0,
        "alphaValues": [],
        "elapsedMilliseconds": 0.0,
        "warningCodes": [code],
        "errorMessage": str(error),
        "algorithmVersion": ALGORITHM_VERSION,
    }
