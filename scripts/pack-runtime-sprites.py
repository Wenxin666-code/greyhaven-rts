"""Bake the desktop sprite archive from the original, editable art exports."""

import argparse
from collections import Counter
from concurrent.futures import ProcessPoolExecutor
from hashlib import sha256
from io import BytesIO
import os
from pathlib import Path
import struct
import uuid

from PIL import Image, features


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art/source"
MAGIC = b"GHBLD008"
SOURCES = (
    "harbor-v01/packed/harbor-v01.pak",
    "infantry-v12/packed/infantry-v12.pak",
    "buildings-v10/packed/structures-v10.pak",
    "buildings-v09/packed/buildings-v09.pak",
    "unit-motion-v09/packed/gait-v09.pak",
)
DESTINATION = ART / "runtime-sprites/packed/runtime-sprites.pak"
CELLS = DESTINATION.with_name("runtime-cells.pak")


def source_frames(path):
    with path.open("rb") as source:
        if source.read(8) != MAGIC:
            raise ValueError(f"Unknown sprite archive format: {path}")
        count, = struct.unpack("<i", source.read(4))
        index = []
        for _ in range(count):
            length, = struct.unpack("<H", source.read(2))
            name = source.read(length).decode("utf-8")
            offset, size = struct.unpack("<qi", source.read(12))
            index.append((name, offset, size))
        start = source.tell()
        for name, offset, size in index:
            source.seek(start + offset)
            image = source.read(size)
            if len(image) != size:
                raise ValueError(f"Truncated sprite frame: {name}")
            yield name, image


def retired(name):
    parts = name.split("/")
    return name.startswith("gait/at/") or (
        len(parts) >= 4 and parts[0] == "b08" and
        (parts[2] == "collapse" or parts[2].startswith(("partial-", "damage-door-")))
    )


def frame_scale(name, full_resolution):
    if full_resolution:
        return 1.0
    if name.startswith(("gait/", "combat/", "stance/")):
        return 2 / 3
    parts = name.split("/")
    if len(parts) >= 4 and parts[0] == "b08" and parts[2] in ("idle", "door", "build", "deploy"):
        return 0.75
    return 1.0


def encode(job):
    original, scale, crop = job
    with Image.open(BytesIO(original)) as source:
        image = source.convert("RGBA")
        if crop:
            image = image.crop(crop)
        if scale < 1:
            size = tuple(max(1, round(dimension * scale)) for dimension in image.size)
            image = image.resize(size, Image.Resampling.LANCZOS)
        with BytesIO() as buffer:
            image.save(buffer, "WEBP", lossless=True, exact=True, method=3, quality=80)
            encoded = buffer.getvalue()
        if scale == 1 and not crop and len(encoded) >= len(original):
            return original
        with Image.open(BytesIO(encoded)) as decoded:
            if decoded.convert("RGBA").tobytes() != image.tobytes():
                raise ValueError("Lossless sprite did not round-trip")
        return encoded


def pack(output, full_resolution, workers):
    names, jobs, hashes = {}, [], {}
    skipped = set()

    def add(name, original, scale, crop=None):
        identity = (sha256(original).digest(), scale, crop)
        if identity not in hashes:
            hashes[identity] = len(jobs)
            jobs.append((original, scale, crop))
        names[name] = hashes[identity]

    for archive in SOURCES:
        for name, image in source_frames(ART / archive):
            if name in names:
                continue
            if retired(name):
                skipped.add(name)
                continue
            add(name, image, frame_scale(name, full_resolution))
    # The manual displays just the front-facing portrait from this older atlas.
    for layer in ("base", "paint"):
        source = ART / "tank-prototype/packed" / f"tank-{layer}.png"
        add(f"tank-portrait-{layer}", source.read_bytes(), 1.0, (0, 7 * 512, 512, 8 * 512))

    print(f"Encoding {len(jobs)} distinct inputs, excluding {len(skipped)} retired names.", flush=True)
    with ProcessPoolExecutor(max_workers=workers) as queue:
        contents = list(queue.map(encode, jobs, chunksize=16))
    write_archive(output, {name: contents[index] for name, index in names.items()})


def encode_sheet(job):
    name, original, size, columns, count = job
    frames = {}
    with Image.open(BytesIO(original)) as source:
        atlas = source.convert("RGBA")
        for number in range(count):
            x, y = number % columns * size, number // columns * size
            if x + size > atlas.width or y + size > atlas.height:
                raise ValueError(f"{name} is too small for cell {number}")
            cell = atlas.crop((x, y, x + size, y + size))
            with BytesIO() as buffer:
                cell.save(buffer, "WEBP", lossless=True, exact=True, method=3)
                frames[f"{name}/{number}"] = buffer.getvalue()
            with Image.open(BytesIO(frames[f"{name}/{number}"])) as decoded:
                if decoded.convert("RGBA").tobytes() != cell.tobytes():
                    raise ValueError(f"{name} cell {number} did not round-trip")
    return frames


def pack_cells(workers):
    jobs = []

    def add(path, size, columns, count):
        jobs.append((Path(path).stem, (ART / path).read_bytes(), size, columns, count))

    for unit in ("arty", "ifv", "ifv-repair", "mcv", "miner", "miner-full"):
        for layer in ("base", "paint"):
            add(f"units-v04/packed/{unit}-{layer}.png", 512, 8, 16)
    for unit in ("arty", "ifv", "mcv", "miner"):
        add(f"units-v04/packed/{unit}-wreck.png", 512, 8, 16)
    for layer in ("base", "paint"):
        add(f"units-v04/packed/engineer-{layer}.png", 512, 8, 8)
        for part in ("hull", "turret"):
            add(f"tank-prototype/packed/tank-{part}-{layer}.png", 256, 8, 64)
        add(f"fieldworks-v01/sprites/wall-{layer}.png", 320, 4, 16)
    add("tank-prototype/packed/tank-wreck.png", 512, 8, 64)
    add("blast-v11/blast-cloud-v11.png", 192, 8, 96)
    add("blast-v11/debris-base-v11.png", 256, 8, 8)
    add("buildings-v10/materials/hazard-flame-v10.png", 128, 8, 96)
    infantry = {f"{unit}-{layer}" for unit in ("squad", "sniper", "at-mobile") for layer in ("base", "paint")}
    for name, original in source_frames(ART / "infantry-v12/packed/infantry-v12.pak"):
        if name in infantry:
            jobs.append((name, original, 512, 8, 8))
            infantry.remove(name)
    if infantry:
        raise ValueError(f"Missing standing sprites: {sorted(infantry)}")

    frames = {}
    with ProcessPoolExecutor(max_workers=workers) as queue:
        for sheet in queue.map(encode_sheet, jobs):
            frames.update(sheet)
    write_archive(CELLS, frames)


def write_archive(output, frames):
    output.parent.mkdir(parents=True, exist_ok=True)
    scratch = output.with_suffix(f".{uuid.uuid4().hex}.tmp")
    table, offsets, payloads = {}, {}, []
    position = 0
    for name, frame in sorted(frames.items()):
        digest = sha256(frame).digest()
        if digest not in offsets:
            offsets[digest] = (position, len(frame))
            payloads.append(frame)
            position += len(frame)
        table[name] = offsets[digest]
    try:
        with scratch.open("wb") as archive:
            archive.write(MAGIC)
            archive.write(struct.pack("<i", len(table)))
            for name, (offset, size) in table.items():
                label = name.encode("utf-8")
                archive.write(struct.pack("<H", len(label)) + label + struct.pack("<qi", offset, size))
            for payload in payloads:
                archive.write(payload)
        os.replace(scratch, output)
    finally:
        if scratch.exists():
            scratch.unlink()
    counts = Counter("WebP" if data[8:12] == b"WEBP" else "PNG" for data in payloads)
    print(f"{len(table)} names, {dict(counts)}, {output.stat().st_size / 2**20:.2f} MiB")


def main():
    arguments = argparse.ArgumentParser(description=__doc__)
    arguments.add_argument("--output", type=Path, default=DESTINATION)
    arguments.add_argument("--full-resolution", action="store_true")
    arguments.add_argument("--cells-only", action="store_true")
    arguments.add_argument("--workers", type=int, default=min(8, os.cpu_count() or 1))
    options = arguments.parse_args()
    if options.workers < 1 or not features.check("webp"):
        arguments.error("Positive worker count and Pillow with WebP support required.")
    if not options.cells_only:
        pack(options.output, options.full_resolution, options.workers)
    pack_cells(options.workers)


if __name__ == "__main__":
    main()
