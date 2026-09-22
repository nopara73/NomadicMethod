#!/usr/bin/env python3
"""Render deterministic whole-loop contact sheets for catalog integrity review."""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
    )
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--frames", type=int, default=10)
    parser.add_argument("--thumbnail-size", type=int, default=128)
    return parser.parse_args()


def require_program(name: str) -> str:
    result = shutil.which(name)
    if not result:
        raise RuntimeError(f"{name} is required.")
    return result


def probe_frame_count(ffprobe: str, video: Path) -> int:
    completed = subprocess.run(
        [
            ffprobe,
            "-v",
            "error",
            "-select_streams",
            "v:0",
            "-count_frames",
            "-show_entries",
            "stream=nb_frames,nb_read_frames",
            "-of",
            "json",
            str(video),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    stream = json.loads(completed.stdout)["streams"][0]
    value = stream.get("nb_frames") or stream.get("nb_read_frames")
    frame_count = int(value)
    if frame_count < 2:
        raise RuntimeError(f"{video} has fewer than two frames.")
    return frame_count


def sample_indices(frame_count: int, sample_count: int) -> list[int]:
    return [
        round(index * (frame_count - 1) / (sample_count - 1))
        for index in range(sample_count)
    ]


def render_strip(
    ffmpeg: str,
    video: Path,
    output: Path,
    indices: list[int],
    thumbnail_size: int,
) -> None:
    selection = "+".join(f"eq(n\\,{index})" for index in indices)
    video_filter = (
        f"select='{selection}',"
        f"scale={thumbnail_size}:{thumbnail_size}:"
        "force_original_aspect_ratio=decrease:flags=lanczos,"
        f"pad={thumbnail_size}:{thumbnail_size}:"
        f"(ow-iw)/2:(oh-ih)/2:color=black,tile={len(indices)}x1"
    )
    subprocess.run(
        [
            ffmpeg,
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(video),
            "-vf",
            video_filter,
            "-frames:v",
            "1",
            "-y",
            str(output),
        ],
        check=True,
    )


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for candidate in (
        Path("C:/Windows/Fonts/arial.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    ):
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def draw_label(
    draw: ImageDraw.ImageDraw,
    exercise: dict,
    y: int,
    width: int,
    label_font: ImageFont.ImageFont,
) -> None:
    block_ids = [block["exerciseId"] for block in exercise["sequenceBlocks"]]
    line_one = (
        f'{exercise["id"]:04d}  {exercise["name"]}  '
        f'd{exercise["muscularDemand"]}  {exercise["mode"]}/'
        f'{exercise["presentation"]}'
    )
    secondaries = ",".join(exercise["secondaryCanonicalGroups"]) or "-"
    line_two = (
        f'P:{exercise["primaryCanonicalGroup"]}  S:{secondaries}  '
        f'side:{exercise["sideSequence"]}  dir:{exercise["directionSequence"]}  '
        f'blocks:{block_ids or "member"}'
    )
    draw.rectangle((0, y, width, y + 58), fill="#f6f1e9")
    draw.text((8, y + 5), line_one, fill="#111111", font=label_font)
    draw.text((8, y + 29), line_two, fill="#333333", font=label_font)


def render_catalog_sheets(args: argparse.Namespace) -> None:
    root = args.repository_root.resolve()
    output = args.output.resolve()
    strips = output / "strips"
    sheets = output / "sheets"
    strips.mkdir(parents=True, exist_ok=True)
    sheets.mkdir(parents=True, exist_ok=True)
    ffmpeg = require_program("ffmpeg")
    ffprobe = require_program("ffprobe")
    catalog_path = root / "NomadicMethod" / "Assets" / "exercises.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
    width = args.frames * args.thumbnail_size
    row_height = 58 + args.thumbnail_size
    label_font = font(15)
    manifest: list[dict] = []

    for item_index, exercise in enumerate(catalog):
        video = root / "NomadicMethod" / "Assets" / exercise["video"]
        frame_count = probe_frame_count(ffprobe, video)
        indices = sample_indices(frame_count, args.frames)
        strip = strips / f'exercise_{exercise["id"]:04d}.png'
        render_strip(ffmpeg, video, strip, indices, args.thumbnail_size)
        sheet_index = item_index // args.batch_size
        manifest.append(
            {
                "exerciseId": exercise["id"],
                "sheet": f"sheet_{sheet_index + 1:03d}.png",
                "row": item_index % args.batch_size,
                "frameCount": frame_count,
                "sampledFrameIndices": indices,
            }
        )

    for sheet_index in range((len(catalog) + args.batch_size - 1) // args.batch_size):
        batch = catalog[
            sheet_index * args.batch_size : (sheet_index + 1) * args.batch_size
        ]
        sheet = Image.new("RGB", (width, row_height * len(batch)), "#ffffff")
        draw = ImageDraw.Draw(sheet)
        for row_index, exercise in enumerate(batch):
            y = row_index * row_height
            draw_label(draw, exercise, y, width, label_font)
            strip_path = strips / f'exercise_{exercise["id"]:04d}.png'
            with Image.open(strip_path) as strip:
                sheet.paste(strip.convert("RGB"), (0, y + 58))
        sheet.save(sheets / f"sheet_{sheet_index + 1:03d}.png", optimize=True)

    (output / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Rendered {len(catalog)} exercises into {len(list(sheets.glob('*.png')))} sheets.")


if __name__ == "__main__":
    render_catalog_sheets(parse_args())
