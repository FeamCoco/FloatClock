# -*- coding: utf-8 -*-
"""
WorldClockBar 品牌图标生成器

由几何定义直接光栅化，不依赖在线素材；每个尺寸单独按「小尺寸加粗」规则渲染，
因此 16px 托盘图标不会像直接缩放那样糊掉。

用法：  python generate-icons.py
输出：  与本脚本同目录
        a-meridian/  b-twin-ring/  c-gauge/
            app-256.png           应用图标预览
            app.ico               多尺寸应用图标 16/20/24/32/40/48/64/128/256
            tray-white.ico        托盘图标（深色任务栏用）
            tray-teal.ico         托盘图标（浅色任务栏用，品牌青）
            tray-ink.ico          托盘图标（浅色任务栏高对比备选，墨色）
            tray-*-32.png         各变体 32px 预览
            mark.svg              纯矢量标记，供设计工具继续编辑
        contact-sheet.png         三方向 × 全尺寸对照表
"""
import io
import math
import os
import struct

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SS = 6  # 超采样倍数：所有几何先放大 SS 倍绘制，再降采样

TEAL_LIGHT = (0x12, 0xA5, 0x94)
TEAL_DARK = (0x0B, 0x5F, 0x58)
BRAND = (0x0F, 0x76, 0x6E)
INK = (0x0A, 0x1F, 0x1D)
WHITE = (0xFF, 0xFF, 0xFF)

APP_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
TRAY_SIZES = [16, 20, 24, 32]
TRAY_BOLD_MAX = 32  # 该尺寸及以下使用加粗几何

# ----------------------------------------------------------------- 绘图工具

def _bbox(cx, cy, r):
    return [(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS]


def stroke_circle(d, cx, cy, r, w, color):
    """描边圆：SVG 是居中描边，Pillow 是向内描边，故半径外推 w/2 对齐。"""
    R = r + w / 2.0
    d.ellipse(_bbox(cx, cy, R), outline=color, width=max(1, round(w * SS)))


def stroke_arc(d, cx, cy, r, a0, a1, w, color):
    R = r + w / 2.0
    d.arc(_bbox(cx, cy, R), a0, a1, fill=color, width=max(1, round(w * SS)))
    cap = w / 2.0
    for a in (a0, a1):
        px = cx + r * math.cos(math.radians(a))
        py = cy + r * math.sin(math.radians(a))
        d.ellipse(_bbox(px, py, cap), fill=color)


def stroke_line(d, p0, p1, w, color):
    d.line([p0[0] * SS, p0[1] * SS, p1[0] * SS, p1[1] * SS],
           fill=color, width=max(1, round(w * SS)))
    cap = w / 2.0
    for p in (p0, p1):
        d.ellipse(_bbox(p[0], p[1], cap), fill=color)


# ------------------------------------------------------------- 三个方向的几何

def mark_a(d, color, bold):
    """A · 子午盘：圆环 + 贯穿的竖直经线"""
    w = 30 if bold else 20
    r = 70 if bold else 74
    top, bot = (68, 188) if bold else (62, 194)
    stroke_circle(d, 128, 128, r, w, color)
    stroke_line(d, (128, top), (128, bot), w, color)


def mark_b(d, color, bold):
    """B · 双环：两环相交，右环降透明度形成层次"""
    w = 30 if bold else 20
    r = 60 if bold else 62
    stroke_circle(d, 98, 128, r, w, color)
    faded = (color[0], color[1], color[2], 140)
    stroke_circle(d, 158, 128, r, w, faded)


def mark_c(d, color, bold):
    """C · 仪表弧：310° 开口环 + 指向缺口的一根指针"""
    w = 30 if bold else 20
    r = 70 if bold else 74
    hand = 54 if bold else 62
    stroke_arc(d, 128, 128, r, 70, 380, w, color)
    d45 = hand / math.sqrt(2)
    stroke_line(d, (128, 128), (128 + d45, 128 + d45), w, color)


MARKS = [
    ("a-meridian", mark_a, "A · 子午盘"),
    ("b-twin-ring", mark_b, "B · 双环"),
    ("c-gauge", mark_c, "C · 仪表弧"),
]


# ------------------------------------------------------------------- 渲染

def render_mark(mark_fn, color, size, bold=False):
    S = 256 * SS
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mark_fn(ImageDraw.Draw(im), color, bold)
    return im.resize((size, size), Image.LANCZOS)


def _gradient(size):
    g = Image.new("RGB", (64, 64))
    px = g.load()
    for y in range(64):
        for x in range(64):
            t = (x + y) / 126.0
            px[x, y] = tuple(
                round(TEAL_LIGHT[i] + (TEAL_DARK[i] - TEAL_LIGHT[i]) * t) for i in range(3)
            )
    return g.resize((size, size), Image.BILINEAR)


def render_app_icon(mark_fn, size):
    S = 256 * SS
    icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, S - 1, S - 1], radius=round(56 / 256.0 * S), fill=255
    )
    icon.paste(_gradient(S), (0, 0), mask)
    inner = round(148 / 256.0 * S)
    icon.paste(render_mark(mark_fn, WHITE, inner), ((S - inner) // 2, (S - inner) // 2),
               render_mark(mark_fn, WHITE, inner))
    return icon.resize((size, size), Image.LANCZOS)


def write_ico(path, images):
    """手写 ICO 容器，允许每个尺寸用各自的几何渲染结果（PNG 载荷）。"""
    payloads = []
    for _, im in images:
        buf = io.BytesIO()
        im.save(buf, format="PNG")
        payloads.append(buf.getvalue())
    header = struct.pack("<HHH", 0, 1, len(images))
    entries, offset = b"", 6 + 16 * len(images)
    for (size, _), payload in zip(images, payloads):
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(payload), offset)
        offset += len(payload)
    with open(path, "wb") as f:
        f.write(header + entries + b"".join(payloads))


# ------------------------------------------------------------------ SVG 源文件

SVG_SOURCE = {
    "a-meridian": (
        '<circle cx="128" cy="128" r="74" stroke="{c}" stroke-width="20"/>'
        '<path d="M128 62 V194" stroke="{c}" stroke-width="20" stroke-linecap="round"/>'
    ),
    "b-twin-ring": (
        '<circle cx="98" cy="128" r="62" stroke="{c}" stroke-width="20"/>'
        '<circle cx="158" cy="128" r="62" stroke="{c}" stroke-width="20" opacity="0.55"/>'
    ),
    "c-gauge": (
        '<path d="M153.3 197.5 A74 74 0 1 1 197.5 153.3" stroke="{c}" '
        'stroke-width="20" stroke-linecap="round"/>'
        '<path d="M128 128 L172 172" stroke="{c}" stroke-width="20" stroke-linecap="round"/>'
    ),
}


def write_svg(path, body, color):
    svg = (
        '<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" '
        'viewBox="0 0 256 256" fill="none">' + body.format(c=color) + "</svg>"
    )
    with open(path, "w", encoding="utf-8") as f:
        f.write(svg)


# -------------------------------------------------------------------- 主流程

def main():
    rows = []
    for slug, fn, label in MARKS:
        out = os.path.join(HERE, slug)
        os.makedirs(out, exist_ok=True)

        app256 = render_app_icon(fn, 256)
        app256.save(os.path.join(out, "app-256.png"))
        write_ico(os.path.join(out, "app.ico"),
                  [(s, render_app_icon(fn, s)) for s in APP_SIZES])

        white32 = render_mark(fn, WHITE, 32, bold=True)
        teal32 = render_mark(fn, BRAND, 32, bold=True)
        white32.save(os.path.join(out, "tray-white-32.png"))
        teal32.save(os.path.join(out, "tray-teal-32.png"))
        render_mark(fn, INK, 32, bold=True).save(os.path.join(out, "tray-ink-32.png"))

        write_ico(os.path.join(out, "tray-white.ico"),
                  [(s, render_mark(fn, WHITE, s, bold=(s <= TRAY_BOLD_MAX))) for s in TRAY_SIZES])
        write_ico(os.path.join(out, "tray-teal.ico"),
                  [(s, render_mark(fn, BRAND, s, bold=(s <= TRAY_BOLD_MAX))) for s in TRAY_SIZES])
        write_ico(os.path.join(out, "tray-ink.ico"),
                  [(s, render_mark(fn, INK, s, bold=(s <= TRAY_BOLD_MAX))) for s in TRAY_SIZES])

        write_svg(os.path.join(out, "mark.svg"), SVG_SOURCE[slug], "#0F766E")
        write_svg(os.path.join(out, "mark-white.svg"), SVG_SOURCE[slug], "#FFFFFF")

        rows.append((label, fn))
        print("ok ->", slug)

    # ---- 对照表 ----
    margin, gap, row_h = 32, 24, 152
    tray_sizes = [64, 48, 32, 24, 20, 16]
    chip_w = len(tray_sizes) * 76
    width = margin + 140 + gap + chip_w + gap + chip_w + margin
    height = margin * 2 + row_h * len(rows)

    sheet = Image.new("RGB", (width, height), (0x10, 0x17, 0x16))
    sd = ImageDraw.Draw(sheet)
    for i, (label, fn) in enumerate(rows):
        y = margin + i * row_h
        app = render_app_icon(fn, 128)
        sheet.paste(app, (margin, y + 12), app)

        chips = (((0x23, 0x2B, 0x2B), WHITE), ((0xEF, 0xF2, 0xF2), INK))
        for j, (chip_bg, color) in enumerate(chips):
            x = margin + 140 + gap + j * (chip_w + gap)
            sd.rounded_rectangle([x, y, x + chip_w, y + row_h - 24], radius=14, fill=chip_bg)
            cx = x + 20
            for s in tray_sizes:
                im = render_mark(fn, color, s, bold=(s <= 40))
                sheet.paste(im, (cx, y + (row_h - 24 - s) // 2), im)
                cx += 76
    sheet.save(os.path.join(HERE, "contact-sheet.png"))
    print("ok -> contact-sheet.png")
    print("done:", HERE)


if __name__ == "__main__":
    main()
