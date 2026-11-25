#!/usr/bin/env python3
"""Create a local CrossUO data profile with the original scale-lined plate art."""

import argparse
from pathlib import Path
import shutil
from struct import Struct, pack_into, unpack_from


TILE_DATA_LENGTH = 3188736
STATIC_BLOCK_START = 512 * 964
STATIC_ROW_SIZE = 41
STATIC_BLOCK_SIZE = 4 + 32 * STATIC_ROW_SIZE
PLATE_IDS = ((0x1415, 0x7FAC), (0x1416, 0x7FAD))
PLATE_MARKER = b"scale-lined plate"
STOCK_ANIMATION = 527
CUSTOM_ANIMATION = 1658
PARTIAL_HUE = 0x00040000

# All forms for which CrossUO draws layered equipment in the world.
EQUIPMENT_BODIES = (
    *range(0x0190, 0x0194),
    *range(0x00B7, 0x00BB),
    *range(0x025D, 0x0261),
    0x029A, 0x029B, 0x02B6, 0x02B7,
    0x03DB, 0x03DF, 0x03E2, 0x02E8, 0x02E9, 0x04E5,
)

ASSETS = {
    ("art", 0x7FAC): ("0x7fac.png", 44, 44),
    ("art", 0x7FAD): ("0x7fad.png", 44, 44),
    ("gumps", 51658): ("51658.png", 260, 237),
    ("gumps", 61658): ("61658.png", 260, 237),
}
# The UOP entry hashes for the two reserved static and paperdoll file names.
ARCHIVE_KEYS = {
    "artlegacymul.uop": {0xE64AF9EE1FB5F607, 0x15FF57E01306EDB4},
    "gumpartlegacymul.uop": {0x9578E39286237364, 0xCAED5C29176160A3},
}
READ_ONLY_ASSET_SUFFIXES = {".def", ".idx", ".iff", ".mul", ".rle", ".uop"}
ARCHIVE_BLOCK = Struct("<IQ")
ARCHIVE_ENTRY = Struct("<QIIIQIH")


def tile_offset(item_id: int) -> int:
    group, row = divmod(item_id, 32)
    return STATIC_BLOCK_START + group * STATIC_BLOCK_SIZE + 4 + row * STATIC_ROW_SIZE


def patch_tiledata(stock: bytes) -> bytes:
    if len(stock) != TILE_DATA_LENGTH:
        raise ValueError("Expected a complete 7.0.9 or later tiledata.mul.")

    if any(unpack_from("<H", stock, tile_offset(i) + 14)[0] == CUSTOM_ANIMATION for i in range(0x10000)):
        raise ValueError(f"Client animation ID {CUSTOM_ANIMATION} is already in use.")

    data = bytearray(stock)
    for original, custom in PLATE_IDS:
        old, new = tile_offset(original), tile_offset(custom)
        if any(stock[new:new + STATIC_ROW_SIZE]):
            raise ValueError(f"Tiledata already contains item ID {custom:#06x}.")
        flags, weight, layer = unpack_from("<QBB", stock, old)
        animation = unpack_from("<H", stock, old + 14)[0]
        if (weight, layer, animation) != (10, 13, STOCK_ANIMATION) or not flags & 0x00400000:
            raise ValueError(f"Unexpected layout for stock plate item {original:#06x}.")
        data[new:new + STATIC_ROW_SIZE] = stock[old:old + STATIC_ROW_SIZE]
        pack_into("<Q", data, new, flags | PARTIAL_HUE)
        pack_into("<H", data, new + 14, CUSTOM_ANIMATION)
        data[new + 21:new + STATIC_ROW_SIZE] = PLATE_MARKER.ljust(20, b"\x00")
    return bytes(data)


def def_values(data: bytes):
    for line in data.decode("ascii", errors="replace").splitlines():
        tokens = line.split("#", 1)[0].split(";", 1)[0].split()
        if tokens and tokens[0][0].isdigit():
            yield tokens


def number(value: str) -> int:
    return int(value, 16 if value.lower().startswith("0x") else 10)


def patch_equipconv(stock: bytes) -> bytes:
    stock_aliases = {}
    for tokens in def_values(stock):
        if len(tokens) < 5:
            continue
        body, item, animation, _, hue = map(number, tokens[:5])
        if item == CUSTOM_ANIMATION:
            raise ValueError(f"Equipconv.def already has an entry for {CUSTOM_ANIMATION}.")
        if item == STOCK_ANIMATION:
            stock_aliases.setdefault(body, (animation, hue))

    eol = b"\r\n" if b"\r\n" in stock else b"\n"
    lines = [b"", b"# Britannia Solo scale-lined plate"]
    for body in EQUIPMENT_BODIES:
        animation, hue = stock_aliases.get(body, (STOCK_ANIMATION, 0))
        lines.append(f"{body}\t{CUSTOM_ANIMATION}\t{animation}\t0\t{hue}".encode("ascii"))
    return stock + eol + eol.join(lines) + eol


def check_archive(archive: Path, reserved: set[int]) -> None:
    size = archive.stat().st_size
    with archive.open("rb") as source:
        header = source.read(28)
        if len(header) != 28 or header[:4] != b"MYP\x00":
            raise ValueError(f"Invalid UOP header in {archive.name}.")
        block = unpack_from("<Q", header, 12)[0]
        visited = set()
        while block:
            if block in visited or block > size - ARCHIVE_BLOCK.size:
                raise ValueError(f"Invalid UOP index in {archive.name}.")
            visited.add(block)
            source.seek(block)
            count, next_block = ARCHIVE_BLOCK.unpack(source.read(ARCHIVE_BLOCK.size))
            if count > (size - source.tell()) // ARCHIVE_ENTRY.size:
                raise ValueError(f"Incomplete UOP index in {archive.name}.")
            for _ in range(count):
                offset, _, _, _, key, _, _ = ARCHIVE_ENTRY.unpack(source.read(ARCHIVE_ENTRY.size))
                if offset and key in reserved:
                    raise ValueError(f"{archive.name} already contains reserved art for the plate.")
            block = next_block


def checked_png(path: Path, width: int, height: int) -> bytes:
    data = path.read_bytes()
    if (
        len(data) < 33
        or data[:16] != b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR"
        or unpack_from(">II", data, 16) != (width, height)
        or data[25] not in (4, 6)
    ):
        raise ValueError(f"{path.name} must be a {width} × {height} PNG with alpha.")
    return data


def build_profile(stock: Path, destination: Path, art: Path) -> None:
    stock = stock.resolve(strict=True)
    art = art.resolve(strict=True)
    destination = destination.parent.resolve(strict=True) / destination.name
    if destination == stock or stock in destination.parents or destination.is_symlink():
        raise ValueError("Choose a separate destination outside the installed client data.")
    if destination.exists():
        raise ValueError(f"Destination already exists: {destination}")

    files = {file.name.lower(): file for file in stock.iterdir()}
    required = {"tiledata.mul", "equipconv.def", "mainmisc.uop", *ARCHIVE_KEYS}
    for name in sorted(required - files.keys()):
        raise ValueError(f"Installed client data is missing {name}.")
    if {"artidx.mul", "gumpidx.mul"} & files.keys():
        raise ValueError("This profile builder expects the modern UOP art and gump archives.")
    for name, keys in ARCHIVE_KEYS.items():
        check_archive(files[name], keys)
    for name, indices in (
        ("art.def", {0x4000 + custom for _, custom in PLATE_IDS}),
        ("gump.def", {index for kind, index in ASSETS if kind == "gumps"}),
    ):
        if name in files and any(number(row[0]) in indices for row in def_values(files[name].read_bytes())):
            raise ValueError(f"{name} already contains a reserved art ID.")

    tiledata = patch_tiledata(files["tiledata.mul"].read_bytes())
    equipconv = patch_equipconv(files["equipconv.def"].read_bytes())
    overlays = files.get("externalimages")
    sprites = {}
    for (kind, image_id), (name, width, height) in ASSETS.items():
        if overlays:
            kind_root = next(
                (path for path in overlays.iterdir() if path.name.lower() == kind), None
            )
            if kind_root and any(
                file.stem.lower() in {str(image_id), hex(image_id)}
                for file in kind_root.rglob("*") if file.is_file()
            ):
                raise ValueError(f"Installed loose graphics already use {kind} ID {image_id}.")
        relative = Path(kind) / name
        sprites[relative] = checked_png(art / "ExternalImages" / relative, width, height)

    destination.mkdir()
    try:
        for name, path in files.items():
            if name not in {"tiledata.mul", "equipconv.def"} and path.is_file() and (
                path.suffix.lower() in READ_ONLY_ASSET_SUFFIXES
                or name.startswith(("cliloc.", "professn."))
                or name in {"client.exe", "mobtypes.txt", "prof.txt"}
            ):
                (destination / path.name).symlink_to(path)
        music = files.get("music")
        if music and music.is_dir():
            shutil.copytree(music, destination / music.name, copy_function=(lambda src, dst: Path(dst).symlink_to(src)))
        images = destination / "ExternalImages"
        if overlays:
            shutil.copytree(overlays, images)
        for path, data in sprites.items():
            target = images / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        (destination / "tiledata.mul").write_bytes(tiledata)
        (destination / "Equipconv.def").write_bytes(equipconv)
    except BaseException:
        shutil.rmtree(destination)
        raise


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("installed_data", type=Path, help="Complete Classic UO game data directory")
    parser.add_argument("destination", type=Path, help="A new profile data directory")
    parser.add_argument(
        "--art", type=Path, default=Path(__file__).parent / "art" / "scale-lined-plate",
        help="Original artwork directory containing ExternalImages",
    )
    args = parser.parse_args()
    try:
        build_profile(args.installed_data, args.destination, args.art)
    except (OSError, ValueError) as error:
        parser.exit(1, f"{error}\n")
    print(f"CrossUO CustomPath and ModernUO first data directory: {args.destination.resolve()}")


if __name__ == "__main__":
    main()
