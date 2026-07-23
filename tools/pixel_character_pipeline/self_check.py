"""Synthetic, network-free verification for pixel_character_pipeline."""

from __future__ import annotations

import csv
import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

from batch_convert import DEFAULT_PRESETS, load_unchanged_record
from contact_sheet import create_comparison_sheet, create_preset_sheets
from pixel_converter import (
    ConversionValidationError,
    convert_character,
    load_preset_document,
    resolve_preset,
    sha256_file,
)


TOOL_DIR = Path(__file__).resolve().parent


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def alpha_and_rgb(path: Path) -> tuple[np.ndarray, np.ndarray]:
    rgba = np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8)
    return rgba[:, :, 3], rgba[:, :, :3]


def mean_saturation(path: Path) -> float:
    rgba = np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8)
    hsv = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2HSV)
    return float(hsv[:, :, 1][rgba[:, :, 3] == 255].mean())


def content_region(path: Path, record: dict[str, object], padding: int) -> np.ndarray:
    rgba = np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8)
    width, height = (int(value) for value in record["contentSize"])
    output_width, output_height = (int(value) for value in record["outputSize"])
    x0 = (output_width - width) // 2
    y0 = output_height - padding - height
    return rgba[y0 : y0 + height, x0 : x0 + width]


def run() -> None:
    document = load_preset_document(DEFAULT_PRESETS)
    base = resolve_preset(document, "world_64", "none", 1.0)
    base.update({"target_content_height": 32, "pixel_block_size": 2, "palette_colors": 8, "padding": 2})

    with tempfile.TemporaryDirectory(prefix="pixel_pipeline_self_check_") as temporary:
        root = Path(temporary)

        # A: transparent character and binary output alpha.
        transparent_source = root / "transparent.png"
        image = Image.new("RGBA", (37, 53), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        draw.rectangle((9, 6, 27, 47), fill=(180, 70, 50, 255))
        draw.rectangle((13, 12, 23, 24), fill=(50, 90, 180, 255))
        image.save(transparent_source)

        website_source = root / "website_source.png"
        website_image = Image.new("RGBA", (40, 60), (0, 0, 0, 0))
        website_draw = ImageDraw.Draw(website_image)
        website_draw.rectangle((10, 8, 29, 47), fill=(180, 70, 50, 255))
        website_draw.rectangle((14, 14, 25, 27), fill=(50, 90, 180, 255))
        website_draw.line((11, 38, 28, 12), fill=(220, 190, 60, 255), width=2)
        website_image.save(website_source)

        # M: source-dimension preservation accepts a null target height and
        # keeps the alpha bounding-box dimensions unchanged.
        website_preset = resolve_preset(document, "website_master")
        require(website_preset["preserve_source_dimensions"] is True, "M: preserve flag was not enabled")
        require(website_preset["target_content_height"] is None, "M: website target height was not normalized to null")
        website_output = root / "website_master" / "transparent.png"
        website_record = convert_character(
            website_source, website_output, "website_master", website_preset, "website_source.png"
        )
        source_bbox_width = website_record["sourceBoundingBox"][2] - website_record["sourceBoundingBox"][0]
        source_bbox_height = website_record["sourceBoundingBox"][3] - website_record["sourceBoundingBox"][1]
        output_bbox_width = website_record["outputBoundingBox"][2] - website_record["outputBoundingBox"][0]
        output_bbox_height = website_record["outputBoundingBox"][3] - website_record["outputBoundingBox"][1]
        require(website_record["targetContentHeight"] is None, "M: sidecar targetContentHeight was not null")
        require(website_record["preserveSourceDimensions"] is True, "M: sidecar preserve flag was incorrect")
        require(website_record["contentSize"] == [source_bbox_width, source_bbox_height], "M: preserved content extent changed")
        require((output_bbox_width, output_bbox_height) == (source_bbox_width, source_bbox_height), "M: source content dimensions changed")
        require(website_record["outputSize"][0] % 4 == 0 and website_record["outputSize"][1] % 4 == 0, "M: preserved canvas was not block-aligned")
        unchanged_website_record = load_unchanged_record(
            website_source, website_output, "website_master", website_preset
        )
        require(unchanged_website_record is not None, "M: null target height broke stable resume hashing")
        require(unchanged_website_record["targetContentHeight"] is None, "M: resumed target height was not null")

        # N: a non-preserving preset must reject a missing target height.
        for invalid_height in (None, 0, -1):
            invalid_preset = dict(document["presets"]["world_64"])
            invalid_preset.update({"preserve_source_dimensions": False, "target_content_height": invalid_height})
            invalid_document = {"presets": {"invalid": invalid_preset}}
            try:
                resolve_preset(invalid_document, "invalid")
                raise AssertionError(f"N: invalid target height unexpectedly passed validation: {invalid_height}")
            except ValueError as exc:
                if invalid_height is None:
                    require("target_content_height is required" in str(exc), "N: missing-height error was not explicit")
                else:
                    require("target_content_height must be greater than 0" in str(exc), "N: non-positive-height error was not explicit")

        # O: existing world presets retain their established PNG bytes.
        expected_world_sha256 = {
            "world_64": "46e6e8e34d99dfa2f97f6db53486f6215804fc44a59b275120f48ee767369e00",
            "world_80": "a79df14bd1ba7382ad68057bbc92f6c220d8077b1276aa4908163b4e47bc1793",
            "world_96": "4bfc3a711e89481fad0ac58df6556c1000697b4bd1d9771521b16ee75dca527f",
            "world_128_detail": "6bb0816f8ed340d4afaf2ab8407a92182ae50219e0256cb3756bf987d3e4ef1b",
        }
        for preset_name, expected_sha256 in expected_world_sha256.items():
            legacy_output = root / "legacy" / f"{preset_name}.png"
            legacy_record = convert_character(
                transparent_source,
                legacy_output,
                preset_name,
                resolve_preset(document, preset_name),
                "transparent.png",
            )
            require(legacy_record["outputSha256"] == expected_sha256, f"O: {preset_name} SHA-256 regression")

        # P: each website dot-size configuration is deterministic across three runs.
        dot_presets = {}
        for block_size in (1, 2, 4):
            dot_preset = dict(website_preset)
            dot_preset["pixel_block_size"] = block_size
            dot_presets[block_size] = dot_preset
            hashes = []
            for run_index in range(3):
                dot_output = root / "dot_determinism" / f"dot{block_size}_{run_index}.png"
                preset_name = "website_master" if block_size == 4 else f"website_dot{block_size}"
                convert_character(website_source, dot_output, preset_name, dot_preset, "website_source.png")
                hashes.append(sha256_file(dot_output))
            require(len(set(hashes)) == 1, f"P: dot size {block_size} was not deterministic")

        # Q: block sizes change the output while keeping the same final extent,
        # and each logical block is uniform after nearest-neighbor expansion.
        dot_records = {}
        dot_hashes = {}
        for block_size, dot_preset in dot_presets.items():
            preset_name = "website_master" if block_size == 4 else f"website_dot{block_size}"
            dot_output = root / "dot_sizes" / f"dot{block_size}.png"
            dot_records[block_size] = convert_character(
                website_source, dot_output, preset_name, dot_preset, "website_source.png"
            )
            dot_hashes[block_size] = sha256_file(dot_output)
            region = content_region(dot_output, dot_records[block_size], int(dot_preset["padding"]))
            height, width = region.shape[:2]
            for y in range(0, height - block_size + 1, block_size):
                for x in range(0, width - block_size + 1, block_size):
                    tile = region[y : y + block_size, x : x + block_size]
                    require(np.all(tile == tile[0, 0]), f"Q: dot size {block_size} has a non-uniform block")
        require(len({tuple(record["outputSize"]) for record in dot_records.values()}) == 1, "Q: dot output canvas sizes differed")
        require(len({tuple(record["contentSize"]) for record in dot_records.values()}) == 1, "Q: dot content sizes differed")
        require(dot_hashes[1] != dot_hashes[4], "Q: dot1 and dot4 output SHA-256 matched")
        require(dot_hashes[2] != dot_hashes[4], "Q: dot2 and dot4 output SHA-256 matched")

        transparent_output = root / "out" / "transparent.png"
        record_a = convert_character(transparent_source, transparent_output, "fixture", base, "transparent.png")
        alpha, rgb = alpha_and_rgb(transparent_output)
        require(set(np.unique(alpha)).issubset({0, 255}), "A: output alpha was not binary")
        require(np.all(rgb[alpha == 0] == 0), "A: transparent background RGB was not cleared")

        # B: hidden RGB pollution must not consume palette slots.
        polluted_source = root / "polluted.png"
        polluted = np.zeros((20, 30, 4), dtype=np.uint8)
        polluted[:, :, :3] = (0, 255, 0)
        polluted[4:16, 5:15] = (180, 40, 40, 255)
        polluted[4:16, 15:25] = (40, 60, 180, 255)
        Image.fromarray(polluted, mode="RGBA").save(polluted_source)
        polluted_preset = dict(base)
        polluted_preset.update({"target_content_height": 12, "pixel_block_size": 1, "palette_colors": 8, "padding": 1})
        polluted_output = root / "out" / "polluted.png"
        record_b = convert_character(polluted_source, polluted_output, "polluted", polluted_preset, "polluted.png")
        alpha_b, rgb_b = alpha_and_rgb(polluted_output)
        opaque_colors = np.unique(rgb_b[alpha_b == 255], axis=0)
        require(record_b["actualPaletteColorCount"] == 2, "B: transparent RGB occupied a palette slot")
        require(not np.any(np.all(opaque_colors == (0, 255, 0), axis=1)), "B: hidden green leaked into output palette")

        # C: three independent outputs must be byte-identical.
        deterministic_hashes = []
        for index in range(3):
            path = root / "determinism" / f"run_{index}.png"
            convert_character(transparent_source, path, "fixture", base, "transparent.png")
            deterministic_hashes.append(sha256_file(path))
        require(len(set(deterministic_hashes)) == 1, "C: repeated output SHA-256 differed")

        # D: non-divisible dimensions preserve content and produce block-multiple canvas.
        odd_source = root / "odd.png"
        odd = Image.new("RGBA", (19, 31), (0, 0, 0, 0))
        ImageDraw.Draw(odd).polygon(((2, 1), (18, 8), (15, 30), (3, 28)), fill=(90, 150, 210, 255))
        odd.save(odd_source)
        odd_preset = dict(base)
        odd_preset.update({"target_content_height": 33, "pixel_block_size": 4, "padding": 3})
        odd_output = root / "out" / "odd.png"
        record_d = convert_character(odd_source, odd_output, "odd", odd_preset, "odd.png")
        require(record_d["outputSize"][0] % 4 == 0 and record_d["outputSize"][1] % 4 == 0, "D: canvas was not a block-size multiple")
        require(record_d["opaquePixelCount"] > 0, "D: odd-size content disappeared")

        # E: requested K larger than unique colors reduces safely.
        solid_source = root / "solid.png"
        Image.new("RGBA", (11, 17), (120, 80, 40, 255)).save(solid_source)
        solid_preset = dict(base)
        solid_preset["palette_colors"] = 32
        solid_output = root / "out" / "solid.png"
        record_e = convert_character(solid_source, solid_output, "solid", solid_preset, "solid.png")
        require(record_e["actualPaletteColorCount"] == 1, "E: unique-color K reduction failed")
        require("PALETTE_REDUCED" in record_e["warningCodes"], "E: palette reduction warning missing")

        # F: empty alpha fails without poisoning subsequent work.
        empty_source = root / "empty.png"
        Image.new("RGBA", (10, 10), (77, 88, 99, 0)).save(empty_source)
        try:
            convert_character(empty_source, root / "out" / "empty.png", "fixture", base, "empty.png")
            raise AssertionError("F: empty alpha unexpectedly succeeded")
        except ConversionValidationError as exc:
            require(exc.code == "EMPTY_ALPHA", "F: empty alpha returned the wrong validation code")
        require(transparent_output.is_file(), "F: prior successful output was lost")

        # G: Unicode input and output names.
        unicode_source = root / "입력_日本語.png"
        image.save(unicode_source)
        unicode_output = root / "출력_日本語" / "용병.png"
        record_g = convert_character(unicode_source, unicode_output, "unicode", base, unicode_source.name)
        require(unicode_output.is_file() and record_g["sourceRelativePath"] == unicode_source.name, "G: Unicode path failed")

        # H: conversion is read-only with respect to source.
        source_hash_before = sha256_file(transparent_source)
        convert_character(transparent_source, root / "source_protection.png", "fixture", base, "transparent.png")
        require(source_hash_before == sha256_file(transparent_source), "H: source SHA-256 changed")

        # I: resume hashes source, preset, algorithm, and output.
        resume_output = root / "resume" / "transparent.png"
        convert_character(transparent_source, resume_output, "resume", base, "transparent.png")
        require(load_unchanged_record(transparent_source, resume_output, "resume", base) is not None, "I: unchanged output did not skip")
        changed_preset = dict(base)
        changed_preset["padding"] += 1
        require(load_unchanged_record(transparent_source, resume_output, "resume", changed_preset) is None, "I: preset change did not invalidate resume")
        changed_image = Image.open(transparent_source).convert("RGBA")
        changed_image.putpixel((10, 10), (1, 2, 3, 255))
        changed_source = root / "changed_source.png"
        changed_image.save(changed_source)
        require(load_unchanged_record(changed_source, resume_output, "resume", base) is None, "I: source change did not invalidate resume")

        # J: workers=1 and workers=2 generate identical bytes and continue past one failed file.
        worker_input = root / "worker_input"
        worker_input.mkdir()
        image.save(worker_input / "good.png")
        Image.new("RGBA", (8, 8), (0, 0, 0, 0)).save(worker_input / "bad.png")
        worker_hashes = []
        for workers in (1, 2):
            worker_output = root / f"worker_output_{workers}"
            report_dir = root / f"worker_reports_{workers}"
            command = [
                sys.executable, str(TOOL_DIR / "batch_convert.py"),
                "--input-dir", str(worker_input), "--output-dir", str(worker_output),
                "--preset", "world_64", "--workers", str(workers), "--force-rebuild",
                "--no-contact-sheet", "--report-dir", str(report_dir),
                "--contact-sheet-dir", str(root / "unused_sheets"),
            ]
            completed = subprocess.run(command, cwd=TOOL_DIR, text=True, capture_output=True, check=False)
            require(completed.returncode == 1, f"J: expected one failed file with workers={workers}: {completed.stdout} {completed.stderr}")
            good_output = worker_output / "world_64" / "good.png"
            require(good_output.is_file(), f"J: good file was not processed with workers={workers}")
            report = json.loads((report_dir / "pixel_conversion_latest.json").read_text(encoding="utf-8"))
            require(report["summary"]["FAILED"] == 1 and report["summary"]["PASS"] + report["summary"]["WARNING"] == 1, "J: batch continuation report was incorrect")
            worker_hashes.append(sha256_file(good_output))
        require(worker_hashes[0] == worker_hashes[1], "J: worker count changed output bytes")

        # K: saturation increases chroma only, preserves achromatic pixels and alpha.
        saturation_source = root / "saturation.png"
        hsv = np.zeros((12, 24, 3), dtype=np.uint8)
        hsv[:, :6] = (0, 60, 180)
        hsv[:, 6:12] = (60, 60, 180)
        hsv[:, 12:18] = (30, 60, 180)
        hsv[:, 18:20] = (0, 0, 255)
        hsv[:, 20:22] = (0, 0, 128)
        hsv[:, 22:24] = (0, 0, 0)
        rgb_fixture = cv2.cvtColor(hsv, cv2.COLOR_HSV2RGB)
        rgba_fixture = np.dstack((rgb_fixture, np.full((12, 24), 255, dtype=np.uint8)))
        Image.fromarray(rgba_fixture, mode="RGBA").save(saturation_source)
        saturation_base = dict(base)
        saturation_base.update({"target_content_height": 12, "pixel_block_size": 1, "palette_colors": 16, "padding": 1})
        none_preset = dict(saturation_base)
        none_preset.update({"increase_saturation": "none", "saturation_multiplier": 1.0})
        high_preset = dict(saturation_base)
        high_preset.update({"increase_saturation": "high", "saturation_multiplier": 1.35})
        none_output = root / "saturation_none.png"
        high_output = root / "saturation_high.png"
        record_high = convert_character(saturation_source, high_output, "saturation_high", high_preset, "saturation.png")
        convert_character(saturation_source, none_output, "saturation_none", none_preset, "saturation.png")
        require(mean_saturation(high_output) > mean_saturation(none_output), "K: high saturation did not increase mean saturation")
        none_rgba = np.asarray(Image.open(none_output).convert("RGBA"), dtype=np.uint8)
        high_rgba = np.asarray(Image.open(high_output).convert("RGBA"), dtype=np.uint8)
        require(np.array_equal(none_rgba[:, :, 3], high_rgba[:, :, 3]), "K: saturation changed alpha")
        none_hsv = cv2.cvtColor(none_rgba[:, :, :3], cv2.COLOR_RGB2HSV)
        high_hsv = cv2.cvtColor(high_rgba[:, :, :3], cv2.COLOR_RGB2HSV)
        active = none_rgba[:, :, 3] == 255
        colored = active & (none_hsv[:, :, 1] > 0)
        achromatic = active & (none_hsv[:, :, 1] == 0)
        require(np.max(np.abs(none_hsv[:, :, 0][colored].astype(int) - high_hsv[:, :, 0][colored].astype(int))) <= 1, "K: hue changed beyond tolerance")
        require(np.max(np.abs(none_hsv[:, :, 2][active].astype(int) - high_hsv[:, :, 2][active].astype(int))) <= 1, "K: value changed beyond tolerance")
        require(np.all(high_hsv[:, :, 1][achromatic] == 0), "K: achromatic colors were tinted")
        repeat_high = root / "saturation_high_repeat.png"
        convert_character(saturation_source, repeat_high, "saturation_high", high_preset, "saturation.png")
        require(sha256_file(high_output) == sha256_file(repeat_high), "K: saturation output was not deterministic")
        require(record_high["saturationAdjustedPixelCount"] > 0, "K: adjusted-pixel diagnostic was zero")

        # L: contact sheets and JSON/CSV artifacts are generated and parseable.
        sheet_dir = root / "sheets"
        preset_sheets = create_preset_sheets([record_a, record_b], sheet_dir, max_per_page=24, columns=4)
        require(len(preset_sheets) == 1 and all(path.is_file() for path in preset_sheets), "L: preset contact sheet was not created")
        comparison_output_root = root / "comparison_outputs"
        comparison_target = comparison_output_root / "fixture" / "transparent.png"
        comparison_target.parent.mkdir(parents=True, exist_ok=True)
        Image.open(transparent_output).save(comparison_target)
        comparison_sheet = create_comparison_sheet(
            root,
            ["transparent.png"],
            ["fixture"],
            comparison_output_root,
            sheet_dir / "comparison.png",
        )
        require(comparison_sheet.is_file(), "L: comparison contact sheet was not created")
        csv_paths = sorted((root / "worker_reports_1").glob("pixel_conversion_*.csv"))
        require(csv_paths, "L: CSV report was not generated")
        with csv_paths[0].open("r", encoding="utf-8-sig", newline="") as stream:
            rows = list(csv.DictReader(stream))
        require(len(rows) == 2, "L: CSV report could not be parsed")

        print("Self-check A PASS: transparent RGBA and binary alpha")
        print("Self-check B PASS: transparent RGB excluded from K-means")
        print("Self-check C PASS: three-run byte determinism")
        print("Self-check D PASS: non-divisible block sizing")
        print("Self-check E PASS: safe palette K reduction")
        print("Self-check F PASS: empty alpha isolation")
        print("Self-check G PASS: Unicode paths")
        print("Self-check H PASS: source SHA-256 protection")
        print("Self-check I PASS: resume invalidation")
        print("Self-check J PASS: workers 1/2 determinism and failure continuation")
        print("Self-check K PASS: saturation, hue/value, achromatic and alpha invariants")
        print("Self-check L PASS: contact sheets and JSON/CSV report parsing")
        print("Self-check M PASS: website_master preserves source dimensions with null target height")
        print("Self-check N PASS: non-preserving presets reject null target height")
        print("Self-check O PASS: world preset PNG SHA-256 regression")
        print("Self-check P PASS: website dot-size three-run determinism")
        print("Self-check Q PASS: dot-size output differences and uniform blocks")


def main() -> int:
    try:
        run()
        print("pixel_character_pipeline self-check PASS")
        return 0
    except Exception as exc:
        print(f"pixel_character_pipeline self-check FAIL: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
