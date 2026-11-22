"""Build the Sundial exterior and texture using Pillow and taniwha's mu.py."""
import argparse
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from mu import (
    Mu, MuColliderMesh, MuMaterial, MuMatTex, MuMesh, MuObject,
    MuRenderer, MuTagLayer, MuTexture, MuTransform,
)


BASE = -0.23
TOP = 10.77
SHELL = [
    (BASE, 1.25), (0.02, 1.47), (0.65, 1.76), (0.98, 1.80),
    (8.95, 1.80), (9.31, 1.76), (9.67, 1.66), (10.00, 1.49),
    (10.28, 1.26), (10.52, 0.94), (10.67, 0.55), (10.75, 0.23), (TOP, 0.06),
]
BANDS = (1.09, 1.92, 2.91, 3.90, 4.89, 5.88, 6.87, 7.86, 8.79)


def add_lathe(mesh, profile, segments, caps):
    ring_start = len(mesh.verts)
    faces = mesh.submeshes[0]
    for row, (height, radius) in enumerate(profile):
        low = profile[max(0, row - 1)]
        high = profile[min(len(profile) - 1, row + 1)]
        delta_r = (high[1] - low[1]) / (high[0] - low[0])
        normal_length = math.sqrt(1 + delta_r * delta_r)
        for segment in range(segments + 1):
            theta = math.tau * segment / segments
            x, y = math.cos(theta), math.sin(theta)
            mesh.verts.append((radius * x, radius * y, height))
            mesh.normals.append((x / normal_length, y / normal_length, -delta_r / normal_length))
            mesh.uvs.append((segment / segments, (height - BASE) / (TOP - BASE)))

    for row in range(len(profile) - 1):
        for segment in range(segments):
            lower = ring_start + row * (segments + 1) + segment
            upper = lower + segments + 1
            faces.extend(((lower, lower + 1, upper), (lower + 1, upper + 1, upper)))

    if caps:
        for end, direction in ((0, -1), (len(profile) - 1, 1)):
            height, radius = profile[end]
            center = len(mesh.verts)
            mesh.verts.append((0, 0, height))
            mesh.normals.append((0, 0, direction))
            mesh.uvs.append((0.25, (height - BASE) / (TOP - BASE)))
            for segment in range(segments):
                theta = math.tau * segment / segments
                mesh.verts.append((radius * math.cos(theta), radius * math.sin(theta), height))
                mesh.normals.append((0, 0, direction))
                mesh.uvs.append((segment / segments, (height - BASE) / (TOP - BASE)))
            for segment in range(segments):
                a, b = center + segment + 1, center + (segment + 1) % segments + 1
                faces.append((center, a, b) if direction > 0 else (center, b, a))


def node(name):
    obj = MuObject(name)
    obj.transform = MuTransform()
    obj.transform.name = name
    obj.transform.localPosition = (0, 0, 0)
    obj.transform.localRotation = (1, 0, 0, 0)
    obj.transform.localScale = (1, 1, 1)
    obj.tag_and_layer = MuTagLayer()
    obj.tag_and_layer.tag, obj.tag_and_layer.layer = "Untagged", 0
    return obj


def visible_mesh(obj, mesh, material):
    obj.shared_mesh = mesh
    obj.renderer = MuRenderer()
    obj.renderer.materials = [material]


def material(name):
    result = MuMaterial()
    result.name = name
    result.shaderName = "KSP/Diffuse"
    tex = MuMatTex()
    tex.index, tex.scale, tex.offset = 0, (1, 1), (0, 0)
    result.textureProperties["_MainTex"] = tex
    return result


def build_texture(path, font_path):
    width = height = 1024
    rng = random.Random(1954)
    pixels = []
    for y in range(height):
        z = TOP - (TOP - BASE) * y / height
        for x in range(width):
            sector = x % 128
            seam = -18 if sector < 2 else 12 if sector < 5 else 0
            shade = seam + int(5 * math.cos(math.tau * x / 128)) + rng.randrange(-6, 7)
            if z < 0.18 or 8.89 < z < 9.12:
                base = (153, 139, 81)
            elif z > 9.12:
                base = (120, 123, 114)
            else:
                base = (77, 84, 76)
            pixels.append(tuple(channel + shade for channel in base))
    image = Image.new("RGB", (width, height))
    image.putdata(pixels)
    draw = ImageDraw.Draw(image)
    for y in (115, 843, 888):
        for x in range(12, width, 32):
            draw.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(159, 153, 135))

    lettering = Image.new("RGBA", (580, 102))
    text = ImageDraw.Draw(lettering)
    large = ImageFont.truetype(str(font_path), 68)
    small = ImageFont.truetype(str(font_path), 23)
    text.text((5, -2), "SUNDIAL", font=large, fill=(221, 215, 184, 255))
    text.text((9, 68), "SD–10   /   TEST ARTICLE", font=small, fill=(201, 185, 138, 255))
    stencil = lettering.crop(lettering.getbbox()).rotate(90, expand=True)
    for x in (218, 730):
        image.paste(stencil, (x, 270), stencil)
    image.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("assets", type=Path)
    parser.add_argument("font", type=Path, help="Path to a bold TTF for the stencilled lettering")
    args = parser.parse_args()
    args.assets.mkdir(parents=True, exist_ok=True)
    mu = Mu("SundialExterior")
    mu.obj = node("SundialExterior")
    hull = MuMesh()
    hull.submeshes = [[]]
    add_lathe(hull, SHELL, 48, caps=True)
    visible_mesh(mu.obj, hull, 0)

    ribs = MuMesh()
    ribs.submeshes = [[]]
    for height in BANDS:
        add_lathe(ribs, [
            (height - 0.084, 1.79), (height - 0.048, 1.875),
            (height + 0.048, 1.875), (height + 0.084, 1.79),
        ], 32, caps=False)
    # Sample the bare steel swatch on the nose for the raised bands.
    ribs.uvs = [(0.17, 0.95)] * len(ribs.verts)
    band_node = node("Reinforcing bands")
    visible_mesh(band_node, ribs, 0)
    mu.obj.children.append(band_node)

    collision = MuMesh()
    collision.submeshes = [[]]
    add_lathe(collision, [SHELL[i] for i in (0, 2, 3, 4, 7, 9, 12)], 12, caps=True)
    collision_node = node("Sundial convex collider")
    collision_node.collider = MuColliderMesh(True)
    collision_node.collider.isTrigger = 0
    collision_node.collider.convex = 1
    collision_node.collider.mesh = collision
    mu.obj.children.append(collision_node)

    hull_texture = MuTexture()
    hull_texture.name, hull_texture.type = "SundialShell.png", 0
    mu.textures = [hull_texture]
    mu.materials = [material("Sundial steel")]
    mu.write(str(args.assets / "SundialExterior.mu"))
    build_texture(args.assets / "SundialShell.png", args.font)


if __name__ == "__main__":
    main()
