"""Bounded-memory contact sheet helpers for pixel_character_pipeline."""

from __future__ import annotations

import math
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw, ImageFont


CHECKER_LIGHT = (216, 216, 216, 255)
CHECKER_DARK = (176, 176, 176, 255)
TEXT_COLOR = (24, 24, 24, 255)


def _checker(size: tuple[int, int], tile: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, CHECKER_LIGHT)
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if (x // tile + y // tile) % 2:
                draw.rectangle((x, y, min(x + tile - 1, size[0] - 1), min(y + tile - 1, size[1] - 1)), fill=CHECKER_DARK)
    return image


def _fit_preview(path: Path, maximum: tuple[int, int], pixel_output: bool) -> Image.Image:
    with Image.open(path) as opened:
        image = opened.convert("RGBA")
    ratio = min(maximum[0] / image.width, maximum[1] / image.height)
    if pixel_output:
        integer_scale = max(1, int(math.floor(ratio)))
        size = (image.width * integer_scale, image.height * integer_scale)
        return image.resize(size, Image.Resampling.NEAREST)
    size = (max(1, round(image.width * ratio)), max(1, round(image.height * ratio)))
    return image.resize(size, Image.Resampling.LANCZOS)


def create_preset_sheets(
    records: Iterable[dict[str, Any]],
    output_dir: Path,
    max_per_page: int = 24,
    columns: int = 6,
) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    successful = [
        record
        for record in records
        if record.get("status") in {"PASS", "WARNING", "SKIPPED"}
        and record.get("outputPath")
        and Path(record["outputPath"]).is_file()
    ]
    if not successful:
        return []
    preset = str(successful[0]["preset"])
    pages: list[Path] = []
    font = ImageFont.load_default()
    preview_max = (192, 192)
    cell_width, cell_height = 216, 224
    for page_index in range(math.ceil(len(successful) / max_per_page)):
        page_records = successful[page_index * max_per_page : (page_index + 1) * max_per_page]
        rows = math.ceil(len(page_records) / columns)
        sheet = _checker((columns * cell_width, rows * cell_height), tile=8)
        draw = ImageDraw.Draw(sheet)
        for index, record in enumerate(page_records):
            column = index % columns
            row = index // columns
            cell_x, cell_y = column * cell_width, row * cell_height
            preview = _fit_preview(Path(record["outputPath"]), preview_max, pixel_output=True)
            x = cell_x + (cell_width - preview.width) // 2
            y = cell_y + 8 + (preview_max[1] - preview.height)
            sheet.alpha_composite(preview, (x, y))
            label = Path(str(record["sourceRelativePath"])).name
            if len(label) > 30:
                label = label[:27] + "..."
            draw.rectangle((cell_x, cell_y + 204, cell_x + cell_width - 1, cell_y + cell_height - 1), fill=(238, 238, 238, 255))
            draw.text((cell_x + 6, cell_y + 208), label, font=font, fill=TEXT_COLOR)
        path = output_dir / f"{preset}_sheet_{page_index + 1:03d}.png"
        sheet.convert("RGB").save(path, format="PNG", compress_level=9)
        pages.append(path)
    return pages


def create_comparison_sheet(
    source_dir: Path,
    relative_paths: list[str],
    presets: list[str],
    output_root: Path,
    output_path: Path,
) -> Path | None:
    if not relative_paths:
        return None
    output_path.parent.mkdir(parents=True, exist_ok=True)
    columns = 1 + len(presets)
    cell_width, cell_height = 196, 196
    header_height = 24
    sheet = _checker((columns * cell_width, header_height + len(relative_paths) * cell_height), tile=8)
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    headers = ["Source"] + presets
    for index, header in enumerate(headers):
        x = index * cell_width
        draw.rectangle((x, 0, x + cell_width - 1, header_height - 1), fill=(238, 238, 238, 255))
        draw.text((x + 6, 7), header, font=font, fill=TEXT_COLOR)
    for row, relative in enumerate(relative_paths):
        paths = [source_dir / relative] + [output_root / preset / relative for preset in presets]
        for column, path in enumerate(paths):
            if not path.is_file():
                continue
            preview = _fit_preview(path, (176, 164), pixel_output=column > 0)
            x = column * cell_width + (cell_width - preview.width) // 2
            y0 = header_height + row * cell_height
            y = y0 + 6 + (164 - preview.height)
            sheet.alpha_composite(preview, (x, y))
            label = Path(relative).name
            if len(label) > 28:
                label = label[:25] + "..."
            draw.rectangle((column * cell_width, y0 + 174, (column + 1) * cell_width - 1, y0 + cell_height - 1), fill=(238, 238, 238, 255))
            draw.text((column * cell_width + 6, y0 + 178), label, font=font, fill=TEXT_COLOR)
    sheet.convert("RGB").save(output_path, format="PNG", compress_level=9)
    return output_path
