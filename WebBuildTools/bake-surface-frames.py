"""Bake geometric texture frames for the atlas. Requires Python 3 and NumPy.

From the project root: python WebBuildTools/bake-surface-frames.py
Add --check to verify the committed resource without changing files.

Each row is two unit axes (long, wide) in atlas mesh-local space, stored as
six UNorm8 components decoded by value * (2/255) - 1. These principal axes
approximate the shape; they do not encode measured muscle fibre trajectories.

The companion distal-tendon table contains two little-endian float32 distances
per row, measured along the decoded long axis oriented towards +Y. A zero pair
means no evidence of a persistent slender distal segment. Boundaries describe
a surface appearance approximation; the mesh has no labelled histology.
"""

import argparse
import mmap
import re
from pathlib import Path

import numpy as np


DISTAL_CANDIDATES = {
    "extensor carpi radialis brevis", "extensor carpi radialis longus", "extensor carpi ulnaris",
    "extensor digitorum", "extensor digitorum longus", "extensor hallucis longus",
    "extensor pollicis brevis", "extensor pollicis longus", "fibularis brevis", "fibularis longus",
    "flexor carpi radialis", "flexor carpi ulnaris", "flexor digitorum longus",
    "flexor digitorum profundus", "flexor digitorum superficialis", "flexor hallucis longus",
    "flexor pollicis longus", "palmaris longus", "tibialis anterior", "tibialis posterior",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def field(block, name):
    match = re.search(rb"(?m)^ +" + name + rb": ([^\r\n]+)", block)
    require(match is not None, f"Missing {name.decode()}")
    return match[1]


def sections(data):
    headings = list(re.finditer(rb"(?m)^--- !u!(\d+) &(-?\d+)\r?$", data))
    result = {}
    for i, heading in enumerate(headings):
        kind, file_id = (int(n) for n in heading.groups())
        end = headings[i + 1].start() if i + 1 < len(headings) else len(data)
        require(file_id not in result, f"Duplicate fileID {file_id}")
        result[file_id] = (kind, heading.end(), end)
    return result


def catalog(data, objects):
    catalogs = [data[start:end] for kind, start, end in objects.values() if kind == 114]
    atlas, = [obj for obj in catalogs if re.search(rb"(?m)^  m_Name: Atlas\r?$", obj)]
    parts = atlas.split(b"\n  parts:", 1)[1].split(b"\n  concepts:", 1)[0]
    records = re.split(rb"(?m)^  - id: ", parts)[1:]
    result = []
    for row in records:
        part_id = row.splitlines()[0].strip()
        match = re.fullmatch(rb"\{fileID: (-?\d+)\}", field(row, b"mesh"))
        require(match is not None, f"Invalid local mesh reference for {part_id!r}")
        result.append((part_id, field(row, b"name"), int(match[1]), field(row, b"system")))
    require(result and len({r[0] for r in result}) == len(result), "Empty or duplicate part IDs")
    require(len({r[2] for r in result}) == len(result), "Shared mesh reference between parts")
    return result, int(field(atlas, b"triangles"))


def mesh_geometry(block, part_id, lut_coordinate):
    require(field(block, b"m_Name").split()[0] == part_id, "Mesh ID differs from catalog")
    index_header = block.split(b"  m_Shapes:", 1)[0]
    require(index_header.count(b"indexCount:") == 1, "Expected a single triangle submesh")
    for attribute in (b"firstByte", b"baseVertex", b"firstVertex", b"topology"):
        require(int(field(index_header, attribute)) == 0, f"Unsupported {attribute!r}")
    count = int(field(index_header, b"indexCount"))
    vertex_count = int(field(block, b"m_VertexCount"))
    require(count > 0 and count % 3 == 0 and vertex_count > 2, "Invalid triangle/vertex count")
    require(vertex_count == int(field(index_header, b"vertexCount")), "Incomplete submesh")

    index_format = int(field(block, b"m_IndexFormat"))
    require(index_format in (0, 1), "Unsupported index format")
    indices = np.frombuffer(bytes.fromhex(field(block, b"m_IndexBuffer").decode()),
                            dtype=("<u2", "<u4")[index_format])
    require(len(indices) == count and indices.max() < vertex_count, "Invalid mesh indices")
    triangles = indices.reshape(-1, 3)

    channels_text = block.split(b"    m_Channels:", 1)[1].split(b"    m_DataSize:", 1)[0]
    channels = [tuple(map(int, c)) for c in re.findall(
        rb"- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)", channels_text)]
    require(channels[0] == (0, 0, 0, 3), "Expected float3 position at offset zero")
    stream, offset, format_id, dimension = channels[5]
    require((stream, format_id, dimension) == (0, 0, 2), "Expected float2 part UV in TEXCOORD1")
    require(all(c[0] == 0 for c in channels if c[3]), "Multiple vertex streams unsupported")
    raw = bytes.fromhex(field(block, b"_typelessdata").decode())
    require(len(raw) == int(field(block, b"m_DataSize")), "Wrong vertex buffer length")
    stride, remainder = divmod(len(raw), vertex_count)
    require(remainder == 0 and stride >= offset + 8, "Invalid vertex stride")
    positions = np.ndarray((vertex_count, 3), "<f4", raw, strides=(stride, 4)).astype(np.float64)
    packed_ids = np.ndarray((vertex_count,), "<f4", raw, offset=offset, strides=(stride,))
    require(np.all(packed_ids == lut_coordinate), "Part UV indices differ from the catalog order")
    require(np.isfinite(positions).all(), "Mesh contains non-finite vertices")
    return positions, triangles


def principal_axes(positions, triangles):
    # Exact second moment of uniform surface area over the triangle mesh. Center
    # first to retain precision for small structures far from the atlas origin.
    corners = (positions - positions.mean(axis=0))[triangles]
    edges = corners[:, 1:] - corners[:, :1]
    area = np.linalg.norm(np.cross(edges[:, 0], edges[:, 1]), axis=1)
    require(area.sum() > 0, "Mesh has zero total surface area")
    weights = area / area.sum()
    summed = corners.sum(axis=1)
    centroid = np.einsum("n,ni->i", weights, summed) / 3
    second = np.einsum("n,ni,nj->ij", weights, summed, summed)
    second += np.einsum("n,nki,nkj->ij", weights, corners, corners)
    covariance = second / 12 - np.outer(centroid, centroid)
    values, axes = np.linalg.eigh(covariance)
    require(np.isfinite(values).all() and values[1] > 0, "Degenerate surface covariance")
    frame = axes[:, [2, 1]].T.copy()
    # Eigenvectors have an arbitrary sign; canonicalise the dominant component.
    for axis in frame:
        if axis[np.argmax(abs(axis))] < 0:
            axis *= -1
    require(np.allclose(frame @ frame.T, np.eye(2), atol=1e-8), "Non-orthonormal PCA frame")
    return np.rint(127.5 * (frame + 1)).clip(0, 255).astype(np.uint8)


def section_perimeters(positions, triangles, axis, levels):
    corners = positions[triangles]
    s = corners @ axis
    order = s.argsort(axis=1)
    p0, p1, p2 = np.take_along_axis(corners, order[:, :, None], axis=1).transpose(1, 0, 2)
    s0, s1, s2 = np.take_along_axis(s, order, axis=1).T
    contour = []
    for level in levels:
        # Intersect the actual triangle surfaces with the plane; summing segment
        # lengths includes all branches when a slice cuts several digit tendons.
        mask = (s0 < level) & (level < s2)
        lo, mid, hi = p0[mask], p1[mask], p2[mask]
        a, b, c = s0[mask], s1[mask], s2[mask]
        u = (level - a) / (c - a)
        q = lo + (hi - lo) * u[:, None]
        below = level < b
        edge_start = np.where(below[:, None], lo, mid)
        edge_end = np.where(below[:, None], mid, hi)
        start = np.where(below, a, b)
        end = np.where(below, b, c)
        r = edge_start + (edge_end - edge_start) * ((level - start) / (end - start))[:, None]
        contour.append(np.linalg.norm(q - r, axis=1).sum())
    return np.array(contour)


def distal_transition(part_name, system, positions, triangles, axis_bytes):
    absent = (0.0, 0.0)
    name = re.sub(r"^(left|right) ", "", part_name.decode().lower())
    if system != b"muscular" or name not in DISTAL_CANDIDATES:
        return absent
    axis = axis_bytes.astype(float) / 127.5 - 1
    axis *= np.sign(axis[1]) / np.linalg.norm(axis)
    s = positions @ axis
    length = np.ptp(s)
    # A short or transverse isolated fragment does not establish distal anatomy.
    if axis[1] < .72 or length < .12:
        return absent
    fractions = np.arange(.045, .96, .025)
    levels = s.min() + length * fractions
    perimeter = section_perimeters(positions, triangles, axis, levels)
    if np.min(perimeter) <= 0:
        return absent
    widths = perimeter / np.quantile(perimeter, .90)
    # Require a narrow plateau, then a sustained belly. This rejects the
    # ordinary short taper of a muscle end and limits colouring to the shaft.
    broad = np.flatnonzero(widths >= .40)
    if len(broad) == 0:
        return absent
    n = int(broad[0])
    if n < 7 or n > 23 or np.median(widths[:n]) > .30 or np.max(widths[1:6]) > .32:
        return absent
    if np.ptp(widths[1:6]) > .16 or np.min(widths[n:n+3]) < .37:
        return absent
    strong = np.flatnonzero(widths[n:] >= .62)
    if not len(strong):
        return absent
    # Blend through the transition as measured, capped to the first 10% of
    # length past the start of widening to avoid marking a long muscle belly.
    return (float(levels[n-1]), float(levels[n + min(int(strong[0]), 3)]))


def bake(source):
    with source.open("rb") as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
        objects = sections(data)
        parts, total_triangles = catalog(data, objects)
        lut_width = 1 << (len(parts) - 1).bit_length()
        rows = []
        counted = 0
        transitions = []
        for index, (part_id, name, mesh_id, system) in enumerate(parts):
            try:
                kind, start, end = objects[mesh_id]
                require(kind == 43, "Asset reference is not a mesh")
                verts, triangles = mesh_geometry(data[start:end], part_id, (index + .5) / lut_width)
                rows.append(principal_axes(verts, triangles))
                transition = distal_transition(name, system, verts, triangles, rows[-1][0])
                transitions.append(transition)
                counted += len(triangles)
            except (KeyError, ValueError) as error:
                raise ValueError(f"Part {index} {part_id.decode()} {name.decode()}: {error}") from error
        require(counted == total_triangles, f"Catalog declares {total_triangles} triangles, found {counted}")
        packed = np.stack(rows)
        decoded = packed.astype(float) / 127.5 - 1
        deviations = abs(np.sum(decoded[:, 0] * decoded[:, 1], axis=1))
        require(deviations.max() < .02, "Quantized axes are not sufficiently orthogonal")
        print(f"{len(rows)} frames, {counted} triangles; largest post-quantization axis dot {deviations.max():.5f}")
        for i, (_, name, _, _) in enumerate(parts):
            if name.lower() in (b"right rectus femoris", b"left sternocleidomastoid", b"right extensor carpi radialis longus"):
                print(f"Row {i}: {name.decode()}, long axis {decoded[i, 0].round(3)}")
            if transitions[i][1] > transitions[i][0]:
                print(f"Distal row {i}: {name.decode()} {np.round(transitions[i], 4)}")
        print(f"{sum(hi > lo for lo, hi in transitions)} measured distal transitions")
        return packed.tobytes(), np.asarray(transitions, dtype="<f4").tobytes()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--distal-output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    frames, distal = bake(args.project / "Assets/@Xyla/Data/Atlas.asset")
    folder = args.project / "Assets/@Xyla/Resources"
    for result, target in ((frames, args.output or folder / "AtlasSurfaceFrames.bytes"),
                           (distal, args.distal_output or folder / "AtlasDistalTendons.bytes")):
        if args.check:
            require(result == target.read_bytes(), f"Surface data out of date: {target}")
        else:
            target.write_bytes(result)
