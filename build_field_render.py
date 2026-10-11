# -*- coding: utf-8 -*-
"""Рендер боевого поля «Рейд» поверх фона стола: проверка компоновки и контраста."""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 2560, 1440
FD = "/usr/share/fonts/truetype/dejavu/"
F_CND = FD + "DejaVuSansCondensed-Bold.ttf"
F_CNR = FD + "DejaVuSansCondensed.ttf"
F_REG = FD + "DejaVuSans.ttf"
def f(path, size): return ImageFont.truetype(path, size)

GOLD = (224, 171, 78); PARCH = (236, 227, 205); MUTED = (155, 142, 172)
OK = (139, 191, 98); BAD = (201, 72, 74); STONE = (107, 101, 92); STONE_D = (74, 70, 64)
EMBER = (224, 140, 60)

TRIBE_ART = {
    "ogres":   ((96, 42, 38), (52, 24, 30), (176, 72, 54)),
    "beasts":  ((54, 62, 96), (28, 30, 54), (92, 128, 164)),
    "vari":    ((40, 66, 86), (22, 34, 48), (84, 140, 168)),
    "ishary":  ((54, 54, 70), (28, 28, 40), (96, 96, 124)),
    "basic":   ((74, 46, 102), (36, 22, 52), (140, 92, 186)),
    "settler": ((48, 82, 92), (24, 42, 52), (96, 162, 176)),
}
RAIL = {"ogres": (150, 40, 32), "beasts": (150, 40, 32), "vari": (58, 98, 130), "ishary": (74, 74, 92)}

def grad(w, h, inner, outer, cx=0.5, cy=0.55):
    s = Image.new("RGB", (28, 40)); px = s.load()
    for y in range(40):
        for x in range(28):
            d = math.hypot((x / 27 - cx) * 1.1, (y / 39 - cy))
            t = max(0.0, min(1.0, d / 0.78)); t = t * t * (3 - 2 * t)
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(3))
    return s.resize((w, h), Image.BICUBIC)

def track(d, x, y, ch, text, fnt, extra):
    """Текст вразрядку — PIL сам letter-spacing не умеет."""
    for c in text:
        d.text((x, y), c, font=fnt, fill=ch)
        x += d.textlength(c, font=fnt) + extra
    return x

def card(w, name="", sub="", strength=None, tribe="basic", back=False,
         magic=False, foot=None, pips=None, ripe=False, leaves=True):
    h = int(w * 1.44)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    rad = max(3, w // 18)

    if back:
        bg = Image.new("RGB", (w, h), (35, 22, 49))
        bd = ImageDraw.Draw(bg)
        for i in range(-h, w, max(5, w // 14)):
            bd.line([(i, h), (i + h, 0)], fill=(27, 17, 38), width=max(3, w // 22))
        m = Image.new("L", (w, h), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, w - 1, h - 1], rad, fill=255)
        im.paste(bg, (0, 0), m)
        d.rounded_rectangle([0, 0, w - 1, h - 1], rad, outline=(10, 6, 16), width=max(1, w // 60))
        inset = int(w * 0.13)
        d.rounded_rectangle([inset, inset, w - inset, h - inset], rad,
                            outline=(224, 171, 78, 70), width=max(1, w // 90))
        return im

    inner, outer, glow = TRIBE_ART.get(tribe, TRIBE_ART["basic"])
    art = grad(w, h, inner, outer)
    sil = Image.new("L", (w, h), 0)
    ad = ImageDraw.Draw(sil)
    import random as _rr
    jit = _rr.Random(hash(name) & 0xffff)
    cx = w * (0.5 + jit.uniform(-.05, .05)); cy = h * (0.62 + jit.uniform(-.03, .03))
    sc = jit.uniform(.92, 1.10)
    if tribe == "settler":
        ad.ellipse([cx - w * .11 * sc, cy - h * .21 * sc, cx + w * .11 * sc, cy - h * .02], fill=255)
        ad.polygon([(cx - w * .21 * sc, h * .93), (cx - w * .09 * sc, cy - h * .04),
                    (cx + w * .09 * sc, cy - h * .04), (cx + w * .21 * sc, h * .93)], fill=255)
        ad.line([(cx - w * .14 * sc, cy + h * .06), (cx - w * .26 * sc, cy + h * .20)],
                fill=255, width=max(3, int(w / 24)))
    elif magic:
        ad.ellipse([cx - w * .22, cy - h * .16, cx + w * .22, cy + h * .16], outline=255, width=max(2, w // 26))
        ad.line([(cx, cy - h * .22), (cx, cy + h * .22)], fill=255, width=max(2, w // 30))
        ad.line([(cx - w * .19, cy - h * .10), (cx + w * .19, cy + h * .10)], fill=255, width=max(2, w // 34))
        ad.line([(cx + w * .19, cy - h * .10), (cx - w * .19, cy + h * .10)], fill=255, width=max(2, w // 34))
    elif tribe == "ogres":
        ad.ellipse([cx - w * .15 * sc, cy - h * .25 * sc, cx + w * .15 * sc, cy - h * .03], fill=255)
        ad.polygon([(cx - w * .33 * sc, h * .94), (cx - w * .15 * sc, cy - h * .07),
                    (cx + w * .15 * sc, cy - h * .07), (cx + w * .33 * sc, h * .94)], fill=255)
        for k in (-1, 1):
            ad.line([(cx + k * w * .28 * sc, cy + h * .01), (cx + k * w * .45 * sc, cy + h * .17)],
                    fill=255, width=max(3, int(w / 18)))
    else:
        ad.polygon([(cx, cy - h * .23 * sc), (cx - w * .13 * sc, cy - h * .10), (cx - w * .28 * sc, h * .94),
                    (cx + w * .28 * sc, h * .94), (cx + w * .13 * sc, cy - h * .10)], fill=255)
        for k in (-1, 1):
            ad.line([(cx + k * w * .12 * sc, cy - h * .17), (cx + k * w * .32 * sc, cy - h * .31)],
                    fill=255, width=max(3, w // 22))
            ad.line([(cx + k * w * .27 * sc, h * .94), (cx + k * w * .43 * sc, cy + h * .04)],
                    fill=255, width=max(3, w // 20))
    halo = sil.filter(ImageFilter.GaussianBlur(w / 11)).point(lambda v: int(v * .70))
    art.paste(Image.new("RGB", (w, h), glow), (0, 0), halo)
    art.paste((11, 9, 17), (0, 0), sil.filter(ImageFilter.GaussianBlur(w / 160)))
    art = art.filter(ImageFilter.GaussianBlur(w / 110))
    m = Image.new("L", (w, h), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, w - 1, h - 1], rad, fill=255)
    im.paste(art, (0, 0), m)
    d = ImageDraw.Draw(im)

    # лента племени слева
    if tribe in RAIL:
        d.rectangle([0, 0, int(w * .085), h], fill=RAIL[tribe] + (235,))
        d.rectangle([int(w * .085), 0, int(w * .10), h], fill=(90, 24, 20, 200))

    # баннер имени
    bh = int(h * .175)
    bn = Image.new("RGBA", (w, bh), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bn)
    for x in range(w):
        a = int(236 * max(0.0, 1 - max(0.0, (x / w - .55)) / .42))
        bd.line([(x, 0), (x, bh)], fill=(10, 6, 16, a))
    im.alpha_composite(bn, (0, 0))

    pad = int(w * .345)
    if w >= 118 and name:
        fs_n = max(9, int(w * .085))
        fn = f(F_CND, fs_n)
        ex = max(0.3, w * .003)
        lim = w - pad - int(w * (.19 if pips else .05))
        nm = name.upper()
        def tw_of(t): return sum(d.textlength(c, font=fn) + ex for c in t)
        if tw_of(nm) > lim:
            while len(nm) > 3 and tw_of(nm + "…") > lim: nm = nm[:-1]
            nm += "…"
        track(d, pad, int(h * .040), GOLD, nm, fn, ex)
        if sub and w >= 150:
            fsub = f(F_CNR, max(8, int(w * .052)))
            track(d, pad, int(h * .040) + fs_n + int(h * .018), (158, 148, 174), sub.upper(), fsub, max(0.3, w * .005))

    # круг силы
    r = int(w * .135)
    ccx, ccy = int(w * .175), int(w * .175)
    d.ellipse([ccx - r, ccy - r, ccx + r, ccy + r], fill=(13, 8, 18, 255),
              outline=(142, 110, 198) if magic else GOLD, width=max(2, int(w * .016)))
    glyph = "✦" if magic else (str(strength) if strength is not None else "–")
    fstr = f(F_CND, int(r * 1.35))
    tw = d.textlength(glyph, font=fstr)
    d.text((ccx - tw / 2, ccy - r * .82), glyph, font=fstr, fill=GOLD if magic else (255, 255, 255))

    # трекер созревания
    if pips:
        n, at, col = pips
        pr = max(4, int(w * .055)); px_ = w - pr - int(w * .05); py = int(w * .11)
        for i in range(n):
            on = (n - i) <= at
            fill = col if on else (12, 8, 18)
            d.ellipse([px_ - pr, py - pr, px_ + pr, py + pr], fill=fill,
                      outline=GOLD if (ripe and on) else (190, 180, 200), width=max(1, int(w * .012)))
            py += pr * 2 + max(2, int(w * .022))

    # каменная плашка свойства
    fh = int(h * (.20 if (foot and w >= 150) else .11))
    d.rounded_rectangle([int(w * .055), h - fh - int(h * .060), w - int(w * .055), h - int(h * .035)],
                        max(2, w // 30), fill=(46, 43, 38))
    d.rounded_rectangle([int(w * .055), h - fh - int(h * .060), w - int(w * .055), h - int(h * .095)],
                        max(2, w // 30), fill=(78, 73, 66))
    if not (foot and w >= 150):
        gy = h - fh - int(h * .060) + fh // 2 - int(h * .012)
        d.ellipse([w / 2 - w * .028, gy, w / 2 + w * .028, gy + w * .056],
                  outline=(150, 118, 58), width=max(1, w // 90))
    if foot and w >= 150:
        ff = f(F_CNR, max(8, int(w * .058)))
        words = foot.split(); lines = []; cur = ""
        for wd in words:
            t = (cur + " " + wd).strip()
            if d.textlength(t, font=ff) > w * .84: lines.append(cur); cur = wd
            else: cur = t
        lines.append(cur)
        ty = h - fh - int(h * .03)
        for ln in lines[:3]:
            tw = d.textlength(ln, font=ff)
            d.text(((w - tw) / 2, ty), ln, font=ff, fill=(26, 22, 16)); ty += int(w * .072)

    # листва по нижней кромке
    if leaves:
        import random as _r
        rr = _r.Random(hash(name) & 0xffff)
        for i in range(max(4, int(w / 13))):
            lx = rr.uniform(0, w); ly = h - rr.uniform(-1, h * .030)
            ls = rr.uniform(w * .030, w * .062); sh = rr.uniform(.25, .75)
            d.ellipse([lx - ls, ly - ls * .6, lx + ls, ly + ls * .6],
                      fill=(int(18 + 40 * sh), int(30 + 56 * sh), int(14 + 24 * sh), 250))

    d.rounded_rectangle([0, 0, w - 1, h - 1], rad, outline=(8, 5, 12), width=max(1, w // 52))
    return im

def shadow(base, im, x, y, blur=14, op=150):
    sh = Image.new("RGBA", base.size, (0, 0, 0, 0))
    a = im.split()[3].point(lambda v: int(v * op / 255))
    sh.paste((0, 0, 0, 255), (x, y + int(blur * .5)), a)
    base.alpha_composite(sh.filter(ImageFilter.GaussianBlur(blur)))

def put(base, im, x, y, sel=False, blur=14):
    shadow(base, im, x, y, blur)
    if sel:
        gl = Image.new("RGBA", base.size, (0, 0, 0, 0))
        gl.paste(GOLD + (255,), (x, y), im.split()[3])
        base.alpha_composite(gl.filter(ImageFilter.GaussianBlur(20)))
        base.alpha_composite(gl.filter(ImageFilter.GaussianBlur(5)))
    base.alpha_composite(im, (x, y))

def put_rot(base, im, cx, by, ang, sel=False):
    r = im.rotate(ang, resample=Image.BICUBIC, expand=True)
    x = int(cx - r.width / 2); y = int(by - r.height)
    put(base, r, x, y, sel, blur=18)

def fan(base, cards, cx, baseline, step, max_ang, sel=()):
    n = len(cards); mid = (n - 1) / 2
    rad = step * 6
    for i, im in enumerate(cards):
        a = -(i - mid) * max_ang
        x = cx + (i - mid) * step
        drop = (1 - math.cos(math.radians(a))) * rad
        y = baseline + drop - (62 if i in sel else 0)
        put_rot(base, im, x, y, a if i not in sel else 0, sel=(i in sel))

def label(d, x, y, text, col=(142, 107, 44), size=19, extra=4.5):
    track(d, x, y, col, text.upper(), f(F_CND, size), extra)

def verdict(base, x, y, text, good=True):
    fnt = f(F_CND, 22)
    tmp = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    tw = int(tmp.textlength(text, font=fnt)) + 26
    chip = Image.new("RGBA", (tw, 34), (0, 0, 0, 0))
    cd = ImageDraw.Draw(chip)
    cd.rounded_rectangle([0, 0, tw - 1, 33], 17, fill=(19, 36, 15, 240) if good else (38, 15, 16, 240),
                         outline=OK if good else BAD, width=2)
    cd.text((13, 4), text, font=fnt, fill=OK if good else BAD)
    shadow(base, chip, x, y, 8, 180)
    base.alpha_composite(chip, (x, y))

def pill(d, x, y, text, state):
    fnt = f(F_CND, 18)
    tw = int(d.textlength(text.upper(), font=fnt) + len(text) * 2.6) + 30
    if state == "now":
        d.rounded_rectangle([x, y, x + tw, y + 34], 17, fill=GOLD)
        track(d, x + 15, y + 7, (18, 12, 5), text.upper(), fnt, 2.6)
    else:
        col = (142, 107, 44) if state == "done" else MUTED
        d.rounded_rectangle([x, y, x + tw, y + 34], 17, outline=col, width=1)
        track(d, x + 15, y + 7, col, text.upper(), fnt, 2.6)
    return x + tw + 10

def grain_track(d, x, y, items, r=13):
    cols = {"dark": (43, 33, 64), "light": (240, 228, 194), "calm": (95, 182, 201)}
    for i, it in enumerate(items):
        cx = x + i * (r * 2 + 9)
        if it: d.ellipse([cx, y, cx + r * 2, y + r * 2], fill=cols[it], outline=(190, 178, 206), width=1)
        else:  d.ellipse([cx, y, cx + r * 2, y + r * 2], outline=(96, 86, 110), width=1)

# ---------------- компоновка ----------------
COLS = {"dark": (43, 33, 64), "light": (240, 228, 194), "calm": (95, 182, 201)}

def render(bg_path, out):
    base = Image.open(bg_path).convert("RGBA").resize((W, H), Image.LANCZOS)
    d = ImageDraw.Draw(base)

    # ===== верхняя строка: противник и фазы
    label(d, 56, 42, "Игрок 2", PARCH, 23, 5)
    grain_track(d, 56, 80, ["dark", "dark", None, None])
    track(d, 56, 122, MUTED, "2 зерна · до победы 2", f(F_CNR, 17), .6)

    x = 1770
    for t, st in (("Обновление", "done"), ("Трофеи", "done"), ("Добор", "done"), ("Действия", "now")):
        x = pill(d, x, 44, t, st)

    # рука противника уходит за верхнюю кромку
    fan(base, [card(126, back=True) for _ in range(5)], 1280, 152, 92, 4.5)

    # ===== поле противника
    label(d, 300, 196, "Пленные противника", (142, 107, 44), 19)
    put(base, card(132, "Тёмный Талант", "Пленный", 2, "settler",
                   pips=(2, 1, COLS["dark"])), 300, 222)
    for i, (nm, stn) in enumerate((("Щенок", 2), ("Обжора", 1))):
        put(base, card(94, nm, "", stn, "beasts", leaves=False), 330 + i * 56, 352, blur=10)
    track(d, 300, 500, MUTED, "защита 2 · суммарная сила 3", f(F_CNR, 18), .6)

    # ===== общие зоны
    yl, yc = 548, 576
    label(d, 108, yl, "Колода", (118, 108, 130), 17)
    put(base, card(124, back=True), 108, yc)
    d.ellipse([200, 728, 258, 766], fill=(13, 8, 18), outline=(58, 44, 78), width=1)
    track(d, 213, 735, PARCH, "38", f(F_CND, 20), 1)

    label(d, 300, yl, "Лес", (118, 108, 130), 17)
    forest = [("Обжора", 1, "beasts", False), ("Вари Отшельник", 3, "vari", False),
              ("Молодой Ишари", 3, "ishary", False), ("Шабаш Зверей", None, "beasts", True)]
    for i, (nm, stn, tb, mg) in enumerate(forest):
        put(base, card(124, nm, "", stn, tb, magic=mg), 300 + i * 136, yc)

    reid = [("Мыслав", 3, "light", 3), ("Морра", 4, "calm", 3), ("Поселенец", 1, "dark", 1)]
    rw, rgap = 152, 24
    rx0 = 1280 - (len(reid) * rw + (len(reid) - 1) * rgap) // 2
    label(d, rx0, yl - 4, "Рейд — цели захвата", GOLD, 22, 5.5)
    for i, (nm, stn, gr, mx) in enumerate(reid):
        put(base, card(rw, nm, "Рейдер" if mx == 3 else "Поселенец", stn, "settler",
                       pips=(mx, 0, COLS[gr])), rx0 + i * (rw + rgap), yc)
        verdict(base, rx0 + i * (rw + rgap) + rw // 2 - 42, yc + int(rw * 1.44) - 18,
                "7 › %d ✓" % stn, True)

    label(d, 1700, yl, "Рейдер", (118, 108, 130), 17)
    put(base, card(124, "Габи", "Рейдер в игре", 5, "settler", pips=(3, 0, COLS["dark"])), 1700, yc)

    label(d, 2310, yl, "Сброс", (118, 108, 130), 17)
    put(base, card(124, back=True), 2310, yc)
    d.ellipse([2402, 728, 2452, 766], fill=(13, 8, 18), outline=(58, 44, 78), width=1)
    track(d, 2417, 735, PARCH, "6", f(F_CND, 20), 1)

    # ===== моё поле
    label(d, 300, 806, "Мои пленные", (142, 107, 44), 19)
    fnt = f(F_CND, 17)
    pw = int(d.textlength("СОЗРЕЛА", font=fnt)) + 34
    d.rounded_rectangle([300, 830, 300 + pw, 864], 17, fill=GOLD)
    track(d, 316, 837, (18, 12, 5), "созрела", fnt, 2.2)
    put(base, card(128, "Поселенка", "Пленная", 1, "settler",
                   pips=(1, 1, COLS["light"]), ripe=True), 300, 874)
    put(base, card(92, "Налётчик", "", 2, "ogres", leaves=False), 328, 1000, blur=10)
    put(base, card(128, "Сестра Оссим", "Пленная", 2, "settler",
                   pips=(2, 1, COLS["calm"])), 500, 874)
    track(d, 300, 1104, MUTED, "защита 1 · сила 2", f(F_CNR, 18), .6)

    label(d, 2240, 806, "Мой трофей", (118, 108, 130), 17)
    put(base, card(124, "Поселенец", "Трофей", 1, "settler", pips=(1, 1, COLS["dark"])), 2240, 834)

    # ===== моя сторона
    # ===== рука
    hand = [("Налётчик", 2, "ogres", False, None), ("Налётчик", 2, "ogres", False, None),
            ("Арош Людоедов", 3, "ogres", False, "Только племенное свойство."),
            ("Глиф Людоедов", None, "ogres", True, "+1 к силе, призыв из Леса."),
            ("Щенок", 2, "beasts", False, "Бонус к силе = зёрнам пленного."),
            ("Круговерт", 6, "basic", False, "Первый ход неуязвим к смертным."),
            ("Гунуити", 8, "basic", False, "Не защищает. Сброс после атаки.")]
    ims = [card(186, nm, "Магия" if mg else "Смертный", stn, tb, magic=mg, foot=ft)
           for nm, stn, tb, mg, ft in hand]
    fan(base, ims, 1356, 1374, 172, 4.0, sel={0, 1, 2})

    # ===== HUD
    hud = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(hud).rounded_rectangle([44, 1150, 620, 1396], 8,
        fill=(17, 11, 25, 224), outline=(62, 48, 84, 255), width=1)
    base.alpha_composite(hud)
    label(d, 72, 1176, "Вы", PARCH, 23, 5)
    d.rounded_rectangle([128, 1174, 228, 1208], 17, fill=GOLD)
    track(d, 144, 1181, (18, 12, 5), "ходит", f(F_CND, 17), 2.2)
    grain_track(d, 72, 1222, ["light", None, None, None], r=11)
    track(d, 220, 1230, MUTED, "победа: 4 любых или 3 одного цвета", f(F_CNR, 17), .6)
    d.line([(72, 1282), (592, 1282)], fill=(62, 48, 84), width=1)
    track(d, 72, 1300, PARCH, "Отряд: 3 карты · сила 7", f(F_CND, 25), 2.4)
    track(d, 72, 1340, (142, 107, 44), "Людоеды · режим атаки", f(F_CNR, 20), .8)
    track(d, 392, 1340, OK, "цель подсвечена", f(F_CNR, 20), .5)

    bx = 2020
    for txt, prim in (("Сброс отряда", False), ("Закончить ход", True)):
        fb = f(F_CND, 21)
        tw = int(d.textlength(txt.upper(), font=fb) + len(txt) * 2.4) + 44
        btn = Image.new("RGBA", (tw, 52), (0, 0, 0, 0))
        ImageDraw.Draw(btn).rounded_rectangle([0, 0, tw - 1, 51], 7,
            fill=(74, 55, 26, 240) if prim else (30, 22, 42, 225),
            outline=GOLD if prim else (72, 56, 94), width=1)
        base.alpha_composite(btn, (bx, 1252))
        track(d, bx + 22, 1266, GOLD if prim else PARCH, txt.upper(), fb, 2.4)
        bx += tw + 16

    base.convert("RGB").save(out, quality=92)
    print("saved", out)

if __name__ == "__main__":
    render(sys.argv[1], sys.argv[2])
