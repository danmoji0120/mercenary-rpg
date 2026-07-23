"""Local recursive CLI for deterministic mercenary character pixelization.

Based on tsutsuji815/pixel_convert (https://github.com/tsutsuji815/pixel_convert),
BSD 2-Clause. Modified for mercenary-rpg batch asset processing.
"""

from __future__ import annotations

import argparse
import csv
import fnmatch
import json
import os
import sys
from concurrent.futures import ProcessPoolExecutor, as_completed
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from contact_sheet import create_comparison_sheet, create_preset_sheets
from pixel_converter import (
    ALGORITHM_VERSION,
    convert_character,
    load_preset_document,
    make_failed_record,
    resolve_preset,
    sha256_file,
    stable_json_hash,
)


TOOL_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = TOOL_DIR.parents[1]
DEFAULT_INPUT = PROJECT_ROOT / "asset_work" / "character_sources"
DEFAULT_OUTPUT = PROJECT_ROOT / "asset_work" / "character_pixelized"
DEFAULT_REPORTS = PROJECT_ROOT / "asset_work" / "character_pixel_reports"
DEFAULT_SHEETS = PROJECT_ROOT / "asset_work" / "character_contact_sheets"
DEFAULT_PRESETS = TOOL_DIR / "presets.json"


def _is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def discover_pngs(
    input_dir: Path,
    output_dir: Path,
    includes: list[str],
    excludes: list[str],
) -> list[tuple[Path, str]]:
    input_resolved = input_dir.resolve()
    output_resolved = output_dir.resolve()
    found: list[tuple[Path, str]] = []
    for path in input_dir.rglob("*"):
        if not path.is_file() or path.suffix.lower() != ".png":
            continue
        resolved = path.resolve()
        if _is_relative_to(resolved, output_resolved):
            continue
        relative = resolved.relative_to(input_resolved).as_posix()
        candidates = (relative, path.name)
        if includes and not any(fnmatch.fnmatchcase(value, pattern) for pattern in includes for value in candidates):
            continue
        if excludes and any(fnmatch.fnmatchcase(value, pattern) for pattern in excludes for value in candidates):
            continue
        found.append((resolved, relative))
    found.sort(key=lambda item: (item[1].casefold(), item[1]))
    return found


def _sidecar_path(output: Path) -> Path:
    return output.with_suffix(output.suffix + ".json")


def load_unchanged_record(
    source: Path,
    output: Path,
    preset_name: str,
    preset: dict[str, Any],
) -> dict[str, Any] | None:
    sidecar = _sidecar_path(output)
    if not output.is_file() or not sidecar.is_file():
        return None
    try:
        record = json.loads(sidecar.read_text(encoding="utf-8"))
        expected_preset_hash = stable_json_hash({"name": preset_name, "config": preset})
        if record.get("sourceSha256") != sha256_file(source):
            return None
        if record.get("presetConfigHash") != expected_preset_hash:
            return None
        if record.get("algorithmVersion") != ALGORITHM_VERSION:
            return None
        if record.get("outputSha256") != sha256_file(output):
            return None
        record["status"] = "SKIPPED"
        record["skippedReason"] = "UNCHANGED_SOURCE_PRESET_AND_ALGORITHM"
        record["elapsedMilliseconds"] = 0.0
        return record
    except (OSError, ValueError, TypeError, json.JSONDecodeError):
        return None


def _worker_convert(job: dict[str, Any]) -> dict[str, Any]:
    try:
        return convert_character(
            job["source"],
            job["output"],
            job["preset_name"],
            job["preset"],
            job["relative"],
        )
    except Exception as exc:
        return make_failed_record(job["source"], job["relative"], job["preset_name"], exc, job["preset"])


def _dry_run_record(job: dict[str, Any]) -> dict[str, Any]:
    source = Path(job["source"])
    return {
        "sourcePath": str(source),
        "sourceRelativePath": job["relative"],
        "sourceSha256": sha256_file(source),
        "preset": job["preset_name"],
        "presetConfigHash": stable_json_hash({"name": job["preset_name"], "config": job["preset"]}),
        "algorithmVersion": ALGORITHM_VERSION,
        "preserveSourceDimensions": bool(job["preset"].get("preserve_source_dimensions", False)),
        "targetContentHeight": job["preset"].get("target_content_height"),
        "status": "SKIPPED",
        "skippedReason": "DRY_RUN",
        "outputPath": job["output"],
        "outputSha256": "",
        "sourceSize": [],
        "sourceBoundingBox": [],
        "contentSize": [],
        "outputSize": [],
        "outputBoundingBox": [],
        "bottomCenterAnchor": [],
        "requestedColors": int(job["preset"]["palette_colors"]),
        "actualColors": 0,
        "alphaValues": [],
        "elapsedMilliseconds": 0.0,
        "warningCodes": [],
        "errorMessage": "",
    }


def write_reports(
    records: list[dict[str, Any]],
    report_dir: Path,
    run_metadata: dict[str, Any],
) -> tuple[Path, Path, Path]:
    report_dir.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S_%fZ")
    json_path = report_dir / f"pixel_conversion_{timestamp}.json"
    csv_path = report_dir / f"pixel_conversion_{timestamp}.csv"
    latest_path = report_dir / "pixel_conversion_latest.json"
    summary = {
        status: sum(1 for record in records if record.get("status") == status)
        for status in ("PASS", "WARNING", "SKIPPED", "FAILED")
    }
    document = {"run": run_metadata, "summary": summary, "records": records}
    payload = json.dumps(document, ensure_ascii=False, indent=2)
    json_path.write_text(payload, encoding="utf-8")
    latest_path.write_text(payload, encoding="utf-8")

    fieldnames = [
        "sourcePath", "sourceRelativePath", "sourceSha256", "sourceHadNoAlpha", "preset",
        "preserveSourceDimensions", "targetContentHeight", "status",
        "skippedReason", "outputPath", "outputSha256", "sourceSize", "sourceBoundingBox", "scaleRatio",
        "contentSize", "outputSize", "outputBoundingBox", "contentBoundingBox", "bottomCenterAnchor", "normalizedPivotX",
        "normalizedPivotY", "requestedColors", "actualColors", "requestedPaletteColorCount",
        "actualPaletteColorCount", "opaquePixelCount", "transparentPixelCount", "sourceUniqueColorEstimate",
        "outputUniqueColorCount", "alphaValues", "elapsedMilliseconds", "warningCodes", "errorMessage",
        "algorithmVersion", "presetConfigHash",
        "saturationPreset", "saturationMultiplier", "saturationAdjustedPixelCount", "saturationClampedPixelCount",
    ]
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for record in records:
            row = dict(record)
            for key, value in tuple(row.items()):
                if isinstance(value, (list, dict)):
                    row[key] = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
            writer.writerow(row)
    return json_path, csv_path, latest_path


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Deterministic transparent character PNG pixelization batch tool.")
    parser.add_argument("--input-dir", type=Path, default=DEFAULT_INPUT)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--preset", default="world_96")
    parser.add_argument("--all-presets", action="store_true")
    parser.add_argument("--limit", type=int)
    parser.add_argument("--include", action="append", default=[])
    parser.add_argument("--exclude", action="append", default=[])
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--workers", type=int, default=1)
    parser.add_argument("--report-dir", type=Path, default=DEFAULT_REPORTS)
    parser.add_argument("--contact-sheet-dir", type=Path, default=DEFAULT_SHEETS)
    parser.add_argument("--no-contact-sheet", action="store_true")
    parser.add_argument("--force-rebuild", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    parser.add_argument("--saturation", choices=("none", "low", "medium", "high"))
    parser.add_argument("--saturation-multiplier", type=float)
    return parser


def run_batch(args: argparse.Namespace) -> tuple[int, dict[str, Any]]:
    input_dir = args.input_dir.resolve()
    output_dir = args.output_dir.resolve()
    report_dir = args.report_dir.resolve()
    sheet_dir = args.contact_sheet_dir.resolve()
    if not input_dir.exists():
        input_dir.mkdir(parents=True, exist_ok=True)
    if not input_dir.is_dir():
        raise ValueError(f"input-dir is not a directory: {input_dir}")
    if input_dir == output_dir:
        raise ValueError("input-dir and output-dir must be different.")
    if args.limit is not None and args.limit < 0:
        raise ValueError("limit must not be negative.")
    if args.workers < 1:
        raise ValueError("workers must be at least 1.")

    document = load_preset_document(DEFAULT_PRESETS)
    preset_names = list(document["presets"].keys()) if args.all_presets else [args.preset]
    presets = {
        name: resolve_preset(document, name, args.saturation, args.saturation_multiplier)
        for name in preset_names
    }
    files = discover_pngs(input_dir, output_dir, args.include, args.exclude)
    if args.limit is not None:
        files = files[: args.limit]
    if args.verbose:
        print(f"Discovered {len(files)} PNG file(s) in deterministic order.")

    jobs: list[dict[str, Any]] = []
    records_by_index: dict[int, dict[str, Any]] = {}
    index = 0
    for preset_name in preset_names:
        preset = presets[preset_name]
        for source, relative in files:
            output = output_dir / preset_name / Path(relative)
            job = {
                "index": index,
                "source": str(source),
                "relative": relative,
                "output": str(output),
                "preset_name": preset_name,
                "preset": preset,
            }
            index += 1
            if args.dry_run:
                records_by_index[job["index"]] = _dry_run_record(job)
                continue
            if not (args.overwrite or args.force_rebuild):
                unchanged = load_unchanged_record(source, output, preset_name, preset)
                if unchanged is not None:
                    records_by_index[job["index"]] = unchanged
                    if args.verbose:
                        print(f"SKIP {preset_name}: {relative}")
                    continue
            jobs.append(job)

    if args.workers == 1:
        for job in jobs:
            record = _worker_convert(job)
            records_by_index[job["index"]] = record
            if args.verbose:
                print(f"{record['status']} {job['preset_name']}: {job['relative']}")
    elif jobs:
        with ProcessPoolExecutor(max_workers=args.workers) as executor:
            futures = {executor.submit(_worker_convert, job): job for job in jobs}
            for future in as_completed(futures):
                job = futures[future]
                try:
                    record = future.result()
                except Exception as exc:
                    record = make_failed_record(job["source"], job["relative"], job["preset_name"], exc, job["preset"])
                records_by_index[job["index"]] = record
                if args.verbose:
                    print(f"{record['status']} {job['preset_name']}: {job['relative']}")

    records = [records_by_index[i] for i in sorted(records_by_index)]
    generated_sheets: list[str] = []
    if not args.no_contact_sheet and not args.dry_run:
        for preset_name in preset_names:
            generated_sheets.extend(
                str(path) for path in create_preset_sheets(
                    (record for record in records if record.get("preset") == preset_name), sheet_dir
                )
            )
        if args.all_presets and 0 < len(files) <= 8:
            comparison = create_comparison_sheet(
                input_dir,
                [relative for _, relative in files],
                preset_names,
                output_dir,
                sheet_dir / "pixel_preset_comparison_test.png",
            )
            if comparison is not None:
                generated_sheets.append(str(comparison))

    run_metadata = {
        "startedUtc": datetime.now(timezone.utc).isoformat(),
        "algorithmVersion": ALGORITHM_VERSION,
        "inputDir": str(input_dir),
        "outputDir": str(output_dir),
        "presets": preset_names,
        "selectedSourceCount": len(files),
        "workers": args.workers,
        "dryRun": bool(args.dry_run),
        "contactSheets": generated_sheets,
    }
    json_path, csv_path, latest_path = write_reports(records, report_dir, run_metadata)
    result = {
        "records": records,
        "jsonReport": str(json_path),
        "csvReport": str(csv_path),
        "latestReport": str(latest_path),
        "contactSheets": generated_sheets,
        "selectedSourceCount": len(files),
    }
    failed = sum(1 for record in records if record.get("status") == "FAILED")
    print(
        f"pixel batch: sources={len(files)} records={len(records)} "
        f"failed={failed} reports={json_path.name},{csv_path.name}"
    )
    return (1 if failed else 0), result


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        code, _ = run_batch(args)
        return code
    except Exception as exc:
        print(f"pixel batch setup failed: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
