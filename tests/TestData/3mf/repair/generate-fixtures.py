#!/usr/bin/env python3
"""Generate deterministic 3MF repair fixtures committed beside this script."""

from pathlib import Path
import zipfile


ROOT = Path(__file__).parent
CORE_NS = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02"
REL_NS = "http://schemas.openxmlformats.org/package/2006/relationships"
FIXED_TIMESTAMP = (2024, 1, 1, 0, 0, 0)

CUBE_VERTICES = (
    (0, 0, 0),
    (1, 0, 0),
    (1, 1, 0),
    (0, 1, 0),
    (0, 0, 1),
    (1, 0, 1),
    (1, 1, 1),
    (0, 1, 1),
)

CUBE_TRIANGLES = (
    (0, 2, 1),
    (0, 3, 2),
    (4, 5, 6),
    (4, 6, 7),
    (0, 1, 5),
    (0, 5, 4),
    (1, 2, 6),
    (1, 6, 5),
    (2, 3, 7),
    (2, 7, 6),
    (3, 0, 4),
    (3, 4, 7),
)


def mesh_xml(vertices, triangles):
    vertex_xml = "".join(
        f'<vertex x="{x}" y="{y}" z="{z}"/>' for x, y, z in vertices
    )
    triangle_xml = "".join(
        f'<triangle v1="{a}" v2="{b}" v3="{c}"/>' for a, b, c in triangles
    )
    return (
        "<mesh><vertices>"
        + vertex_xml
        + "</vertices><triangles>"
        + triangle_xml
        + "</triangles></mesh>"
    )


def model_xml(objects, build_items, metadata="", resources=""):
    return (
        '<?xml version="1.0" encoding="UTF-8"?>'
        + f'<model unit="millimeter" xmlns="{CORE_NS}">'
        + metadata
        + "<resources>"
        + resources
        + "".join(objects)
        + "</resources><build>"
        + "".join(build_items)
        + "</build></model>"
    )


def object_xml(object_id, mesh, attributes=""):
    return f'<object id="{object_id}" type="model"{attributes}>{mesh}</object>'


def write_package(name, model):
    contents = {
        "[Content_Types].xml": (
            '<?xml version="1.0" encoding="UTF-8"?>'
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            '<Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/>'
            "</Types>"
        ),
        "_rels/.rels": (
            '<?xml version="1.0" encoding="UTF-8"?>'
            + f'<Relationships xmlns="{REL_NS}">'
            '<Relationship Id="rel0" '
            'Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel" '
            'Target="/3D/model.model"/>'
            "</Relationships>"
        ),
        "3D/model.model": model,
    }
    with zipfile.ZipFile(ROOT / name, "w", compression=zipfile.ZIP_DEFLATED) as package:
        for entry_name, content in contents.items():
            entry = zipfile.ZipInfo(entry_name, FIXED_TIMESTAMP)
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o644 << 16
            package.writestr(entry, content.encode("utf-8"))


def cube_object(object_id=1, triangles=CUBE_TRIANGLES, attributes=""):
    return object_xml(object_id, mesh_xml(CUBE_VERTICES, triangles), attributes)


def main():
    write_package(
        "valid-closed-cube.3mf",
        model_xml([cube_object()], ['<item objectid="1"/>']),
    )
    write_package(
        "degenerate-triangles.3mf",
        model_xml(
            [cube_object(triangles=CUBE_TRIANGLES + ((0, 0, 1),))],
            ['<item objectid="1"/>'],
        ),
    )
    write_package(
        "duplicate-triangles.3mf",
        model_xml(
            [cube_object(triangles=CUBE_TRIANGLES + (CUBE_TRIANGLES[0],))],
            ['<item objectid="1"/>'],
        ),
    )
    write_package(
        "flipped-orientation.3mf",
        model_xml(
            [cube_object(triangles=((0, 1, 2),) + CUBE_TRIANGLES[1:])],
            ['<item objectid="1"/>'],
        ),
    )
    write_package(
        "open-mesh.3mf",
        model_xml([cube_object(triangles=CUBE_TRIANGLES[1:])], ['<item objectid="1"/>']),
    )
    write_package(
        "non-manifold-edge.3mf",
        model_xml(
            [cube_object(triangles=CUBE_TRIANGLES + ((0, 1, 4),))],
            ['<item objectid="1"/>'],
        ),
    )
    write_package(
        "combined-safe-and-unsafe-issues.3mf",
        model_xml(
            [
                cube_object(
                    triangles=CUBE_TRIANGLES[1:] + (CUBE_TRIANGLES[1], (0, 0, 1))
                )
            ],
            ['<item objectid="1"/>'],
        ),
    )
    metadata = '<metadata name="fixture">multi-object metadata retained</metadata>'
    resources = '<basematerials id="5"><base name="fixture green" displaycolor="#00AA55"/></basematerials>'
    second_vertices = tuple((x + 3, y, z) for x, y, z in CUBE_VERTICES)
    second_mesh = mesh_xml(second_vertices, CUBE_TRIANGLES + (CUBE_TRIANGLES[0],))
    write_package(
        "multi-object-with-metadata.3mf",
        model_xml(
            [
                cube_object(1, attributes=' pid="5" pindex="0"'),
                object_xml(2, second_mesh, ' pid="5" pindex="0"'),
            ],
            ['<item objectid="1"/>', '<item objectid="2"/>'],
            metadata,
            resources,
        ),
    )


if __name__ == "__main__":
    main()
