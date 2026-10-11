# -*- coding: utf-8 -*-
"""Заставка «Рейд», 3 секунды.

Ноги не вырезаются из картинки, а рисуются заново в каждом кадре: цикл шага
с контактом, приседом, проходящей позой и подъёмом, перекат стопы с пятки на
носок, таз ходит вверх-вниз. Корпус — исходный рисунок, но он наклоняется,
качается и тащит за собой подол через сеточную деформацию.
Финал — сам логотип: фигуры, рамка штрихами, название.
"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

BASE = os.path.dirname(os.path.abspath(__file__))
CON = os.path.join(BASE, "concepts")
OUT = os.path.join(CON, "Клод")

W, H = 696, 1044
FPS, DUR = 24, 3.0
NF = int(FPS * DUR)
BG = (32, 33, 37)
SAND = (250, 193, 126)
GROUND = 588
RED_X, SAND_X = 245, 468

def lerp(a, b, t): return a + (b - a) * t
def clamp01(t): return max(0.0, min(1.0, t))
def ease_io(t): t = clamp01(t); return t * t * (3 - 2 * t)
def ease_out(t, p=2.0): return 1 - (1 - clamp01(t)) ** p

# ======================= разбор рисунка =======================
def load_char(path, target_h, facing, hem_frac, measure_frac, flip=False,
              jag=0, hip_frac=0.55):
    """Корпус — рисунок, обрезанный по подолу (при jag>0 — зубцом, как на тунике).
    Ширину, цвет и расстановку ног меряем внизу рисунка, где они настоящие."""
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getbbox())
    im = im.resize((max(1, int(im.width * target_h / im.height)), target_h), Image.LANCZOS)
    w, h = im.size
    ap = im.split()[3].point(lambda v: 255 if v > 110 else 0).load()
    px = im.convert("RGB").load()

    def runs_at(y):
        out, x = [], 0
        while x < w:
            if ap[x, y]:
                x2 = x
                while x2 < w and ap[x2, y]: x2 += 1
                if x2 - x > 2: out.append((x, x2))
                x = x2
            else: x += 1
        return out

    my = min(h - 2, int(h * measure_frac))
    runs = runs_at(my)
    if len(runs) < 2:
        c = w / 2; runs = [(c - 20, c - 4), (c + 4, c + 20)]
    runs = sorted(runs, key=lambda r: r[0])[:2]
    hips = [(a_ + b_) / 2 - w / 2 for a_, b_ in runs]
    leg_w = max(11.0, 1.55 * sum(b_ - a_ for a_, b_ in runs) / 2)
    samples = []
    for a_, b_ in runs:
        for dy in (-6, -3, 0, 3, 6):
            yy = max(1, min(h - 2, my + dy))
            for fx in (.35, .5, .65):
                xx = int(max(1, min(w - 2, a_ + (b_ - a_) * fx)))
                if ap[xx, yy]: samples.append(px[xx, yy])
    samples.sort(key=lambda c: c[0] + c[1] + c[2])
    col = samples[len(samples) // 2] if samples else (250, 193, 126)

    hem = int(h * hem_frac)
    body = im.crop((0, 0, w, min(h, hem + max(2, int(h * .01))))).copy()
    if jag:
        bw, bh = body.size
        m = Image.new("L", (bw, bh), 255)
        md = ImageDraw.Draw(m)
        teeth = []
        for k in range(jag + 1):
            tx = bw * k / jag
            ty = bh - (bh * .055 if k % 2 else 0)
            teeth.append((tx, ty))
        md.polygon([(0, bh)] + teeth + [(bw, bh)], fill=0)
        al = body.split()[3]
        body.putalpha(Image.composite(al, Image.new("L", (bw, bh), 0), m))

    return dict(art=body, w=w, h=h, hem=hem, hips=hips, leg_w=leg_w, color=col,
                facing=facing, flip=flip, shin=h - hem, full_leg=h * (1 - hip_frac))

# ======================= ноги рисуются заново =======================
def ik(hip, foot, l1, l2, knee_dir):
    dx, dy = foot[0] - hip[0], foot[1] - hip[1]
    dist = min(math.hypot(dx, dy), (l1 + l2) * .995) or .01
    a = math.atan2(dy, dx)
    cb = max(-1.0, min(1.0, (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)))
    ang = a + knee_dir * math.acos(cb)
    return (hip[0] + math.cos(ang) * l1, hip[1] + math.sin(ang) * l1)

def taper(d, p0, p1, w0, w1, col):
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L, dx / L
    d.polygon([(p0[0] + nx * w0, p0[1] + ny * w0), (p1[0] + nx * w1, p1[1] + ny * w1),
               (p1[0] - nx * w1, p1[1] - ny * w1), (p0[0] - nx * w0, p0[1] - ny * w0)], fill=col)

def boot(d, ankle, ang, L, w, col, face):
    pts = [(-.40, -1.00), (.40, -1.00), (.95, -.30), (1.00, .12), (-.52, .14), (-.62, -.34)]
    ca, sa = math.cos(ang), math.sin(ang)
    d.polygon([(ankle[0] + px_ * L * face * ca - py_ * w * sa,
                ankle[1] + px_ * L * face * sa + py_ * w * ca) for px_, py_ in pts], fill=col)

def advance(u):
    """Шаг бойца, а не пешехода: 70 % фазы стопа стоит, 30 % — короткий перенос у земли."""
    u = u - math.floor(u)
    if u < .70:
        return 0.0, 0.0, 0.0
    t = (u - .70) / .30
    return ease_io(t), math.sin(math.pi * t) ** .8, math.sin(math.pi * t) * .20

def draw_char(canvas, ch, x, ground, cyc, stride, lean, press=0.0):
    """cyc — фаза цикла (1.0 = полный двойной шаг). Стопы ведут, таз следует за ними."""
    face = ch["facing"]
    full = ch["full_leg"]
    shin = ch["shin"]

    # стопы: каждая переносится в свою половину цикла, остальное время стоит
    feet = []
    for i in range(2):
        u = cyc + .5 * i
        k = math.floor(u)
        adv, lift, roll = advance(u)
        base = (k - .5 * i) * stride + (stride * .5 if i else 0.0)
        fx = base + adv * stride
        feet.append((fx, lift, roll))

    offs = [f[0] - cyc * stride for f in feet]
    mid = sum(offs) / 2
    offs = [o - mid for o in offs]                 # стопы расходятся вокруг корпуса
    spread = max(abs(o) for o in offs)
    sink = math.sqrt(max(1.0, (full * .94) ** 2 - spread ** 2))
    hip_y = ground - sink - press
    stand = [offs[i] for i in range(2) if feet[i][1] < .05]
    bias = (sum(stand) / len(stand)) * .20 if stand else 0.0
    hip_x = x + bias * face

    hem_y = hip_y + (ch["hem"] - ch["h"] * .55)
    sway = math.sin(2 * math.pi * cyc - 1.2) * shin * .22

    legs = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(legs)
    order = sorted(range(2), key=lambda i: offs[i] * face)
    for n, i in enumerate(order):
        lift, roll = feet[i][1], feet[i][2]
        ax = x + offs[i] * face
        ay = ground - lift * shin * .20
        top = (hip_x + ch["hips"][i] * .50 * face, hem_y - shin * .18)
        l1 = (ground - top[1]) * .52
        l2 = (ground - top[1]) * .50
        dd = math.hypot(ax - top[0], ay - top[1])
        if dd > (l1 + l2) * .97:
            k = (l1 + l2) * .97 / dd
            ax = top[0] + (ax - top[0]) * k
            ay = top[1] + (ay - top[1]) * k
        knee = ik(top, (ax, ay), l1, l2, -face)
        far = (n == 0)
        shade = tuple(min(255, int(v * (.84 if far else 1.0))) for v in ch["color"])
        taper(ld, top, knee, ch["leg_w"] * .80, ch["leg_w"] * .60, shade)
        taper(ld, knee, (ax, ay), ch["leg_w"] * .60, ch["leg_w"] * .42, shade)
        ld.ellipse([knee[0] - ch["leg_w"] * .58, knee[1] - ch["leg_w"] * .58,
                    knee[0] + ch["leg_w"] * .58, knee[1] + ch["leg_w"] * .58], fill=shade)
        boot(ld, (ax, ay), roll * face, shin * .40, ch["leg_w"] * .50, shade, face)
    canvas.alpha_composite(legs)

    art = ch["art"]
    if ch["flip"]: art = art.transpose(Image.FLIP_LEFT_RIGHT)
    pad = int(art.height * .45)
    canv = Image.new("RGBA", (art.width + pad * 2, art.height + pad * 2), (0, 0, 0, 0))
    canv.paste(art, (pad, pad))
    canv = sway_mesh(canv, sway * face, pad + art.height * .42)
    pivot_y = pad + ch["h"] * .55
    canv = canv.rotate(-lean, resample=Image.BICUBIC,
                       center=(canv.width / 2, pivot_y), fillcolor=(0, 0, 0, 0))
    canvas.alpha_composite(canv, (int(hip_x - canv.width / 2), int(hip_y - pivot_y)))

def sway_mesh(img, amount, pivot_y, bands=16):
    if abs(amount) < .4: return img
    w, h = img.size
    data = []
    for i in range(bands):
        y0 = int(h * i / bands); y1 = int(h * (i + 1) / bands)
        f = clamp01((y0 - pivot_y) / max(1.0, h - pivot_y))
        dx = amount * (f ** 1.8)
        data.append(((0, y0, w, y1), (-dx, y0, -dx, y1, w - dx, y1, w - dx, y0)))
    return img.transform((w, h), Image.MESH, data, Image.BILINEAR)

# ======================= логотип =======================
def logo_parts():
    lg = Image.open(os.path.join(CON, "Рейд.jpg")).convert("RGB").resize((W, H), Image.LANCZOS)
    px = lg.load()
    nonbg = Image.new("L", (W, H), 0); np_ = nonbg.load()
    for y in range(H):
        for x in range(W):
            r, g, b = px[x, y]
            if abs(r - BG[0]) + abs(g - BG[1]) + abs(b - BG[2]) > 70: np_[x, y] = 255
    fr = Image.new("L", (W, H), 0); fp = fr.load()
    def runs_h(y0, y1, ml):
        for y in range(y0, y1):
            x = 0
            while x < W:
                if np_[x, y]:
                    x2 = x
                    while x2 < W and np_[x2, y]: x2 += 1
                    if x2 - x >= ml:
                        for xx in range(x, x2): fp[xx, y] = 255
                    x = x2
                else: x += 1
    def runs_v(x0, x1, ml):
        for x in range(x0, x1):
            y = 0
            while y < H:
                if np_[x, y]:
                    y2 = y
                    while y2 < H and np_[x, y2]: y2 += 1
                    if y2 - y >= ml:
                        for yy in range(y, y2): fp[x, yy] = 255
                    y = y2
                else: y += 1
    side = {}
    for name, (fn, a, b, ml) in dict(top=(runs_h, 158, 204, 150), bottom=(runs_h, 856, 900, 150),
                                     rule=(runs_h, 586, 634, 150), left=(runs_v, 104, 150, 150),
                                     right=(runs_v, 582, 622, 150)).items():
        before = fr.copy(); fn(a, b, ml)
        diff = Image.new("L", (W, H), 0); dp = diff.load(); bp = before.load()
        for y in range(H):
            for x in range(W):
                if fp[x, y] and not bp[x, y]: dp[x, y] = 255
        side[name] = diff
    title = Image.new("L", (W, H), 0); tp = title.load()
    figs = Image.new("L", (W, H), 0); gp = figs.load()
    for y in range(H):
        for x in range(W):
            if np_[x, y] and not fp[x, y]:
                (tp if y > 636 else gp)[x, y] = 255
    return lg, figs, side, title

# ======================= сборка =======================
def build():
    os.makedirs(OUT, exist_ok=True)
    red = load_char(os.path.join(CON, "raid-character-left-start.png"), 397, +1, 0.80, 0.94, flip=True)
    sand = load_char(os.path.join(CON, "raid-character-right-start.png"), 392, -1, 0.72, 0.90, flip=False, jag=5)
    for n, c in (("красный", red), ("рыцарь", sand)):
        print("  %s: подол %d, голень %d, нога %.0f, ширина %.0f" %
              (n, c["hem"], c["shin"], c["full_leg"], c["leg_w"]))
    lg, figs, side, title = logo_parts()

    CREEP, LUNGE, HIT = 0.44, 0.60, 0.635
    R_STRIDE, S_STRIDE = 46.0, 52.0
    frames = []
    for i in range(NF):
        t = i / (NF - 1)
        img = Image.new("RGBA", (W, H), BG + (255,))
        d = ImageDraw.Draw(img)
        g = int(clamp01(t / .25) * 36)
        d.line([(50, GROUND + 10), (646, GROUND + 10)], fill=(32 + g, 33 + g, 37 + g), width=3)

        if t < HIT:
            # крадутся, замирают, потом бросок
            c = clamp01(t / CREEP)
            creep = ease_io(c) * 0.86
            hold = clamp01((t - CREEP) / (LUNGE - CREEP))
            lunge = ease_out(clamp01((t - LUNGE) / (HIT - LUNGE)), 1.5)
            prog = creep + lunge * 0.42
            press = 5.0 * math.sin(math.pi * hold) * (1 - lunge)      # подсед перед броском

            rd = prog * 3.4
            sd = prog * 3.1
            rx = lerp(-40, RED_X, prog / 1.28)
            sx = lerp(760, SAND_X, prog / 1.28)
            shake = math.sin((t - HIT) * 300) * 5 if t > HIT - .02 else 0
            lean_r = 3.0 + 7.0 * lunge
            lean_s = 3.5 + 8.0 * lunge
            draw_char(img, red, rx - shake, GROUND, rd, R_STRIDE, lean_r, press)
            draw_char(img, sand, sx + shake, GROUND, sd, S_STRIDE, lean_s, press * .8)
        else:
            img.paste(lg, (0, 0), figs)
            fr = clamp01((t - HIT - .03) / .16)
            for name, order in (("top", 0), ("right", 1), ("bottom", 2), ("left", 3)):
                p = clamp01(fr * 3.6 - order * .85)
                if p <= 0: continue
                m = side[name]
                if p < 1:
                    cut = Image.new("L", (W, H), 0)
                    if name in ("top", "bottom"):
                        x0, x1 = (0, int(W * p)) if name == "top" else (W - int(W * p), W)
                        cut.paste(m.crop((x0, 0, x1, H)), (x0, 0))
                    else:
                        y0, y1 = (0, int(H * p)) if name == "right" else (H - int(H * p), H)
                        cut.paste(m.crop((0, y0, W, y1)), (0, y0))
                    m = cut
                img.paste(lg, (0, 0), m)
            rp = clamp01((t - HIT - .05) / .08)
            if rp > 0:
                m = side["rule"]
                if rp < 1:
                    cut = Image.new("L", (W, H), 0); cut.paste(m.crop((0, 0, int(W * rp), H)), (0, 0)); m = cut
                img.paste(lg, (0, 0), m)
            tp = clamp01((t - HIT - .15) / .13)
            if tp > 0:
                m = title
                if tp < 1:
                    cut = Image.new("L", (W, H), 0)
                    cut.paste(m.crop((0, 0, int(W * clamp01(tp * 1.1)), H)), (0, 0)); m = cut
                img.paste(lg, (0, 0), m)

        if HIT - .03 < t < HIT + .03:
            k = 1 - abs(t - HIT) / .03
            img = Image.blend(img.convert("RGB"), Image.new("RGB", (W, H), SAND), k * .26).convert("RGBA")
        frames.append(img.convert("RGB"))

    for i, f in enumerate(frames):
        f.save(os.path.join(OUT, "frame_%03d.png" % i))
    frames[0].save(os.path.join(OUT, "raid-intro.gif"), save_all=True, append_images=frames[1:],
                   duration=int(1000 / FPS), loop=0, optimize=True)
    sheet = Image.new("RGB", (6 * 232, 2 * 348), "black")
    for k, i in enumerate((2, 8, 14, 20, 26, 32, 38, 44, 50, 58, 64, 71)):
        sheet.paste(frames[i].resize((232, 348)), ((k % 6) * 232, (k // 6) * 348))
    sheet.save(os.path.join(OUT, "_contact.jpg"), quality=88)
    strip = Image.new("RGB", (10 * 200, 300), BG)
    for k, i in enumerate(range(18, 38, 2)):
        strip.paste(frames[i].crop((40, 150, 656, 920)).resize((200, 250)), (k * 200, 25))
    strip.save(os.path.join(OUT, "_walk.jpg"), quality=92)
    print("кадров:", len(frames), "gif:", os.path.getsize(os.path.join(OUT, "raid-intro.gif")))

if __name__ == "__main__":
    build()
