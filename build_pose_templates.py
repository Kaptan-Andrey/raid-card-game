# -*- coding: utf-8 -*-
"""Шаблоны для отрисовки ключевых поз: тот же холст, та же земля, тот же масштаб.
Рисовать поверх призрака — тогда кадры соберутся без подгонки."""
import os
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.dirname(os.path.abspath(__file__))
CON = os.path.join(BASE, "concepts")
OUT = os.path.join(CON, "Клод", "позы")
W, H, GROUND = 696, 1044, 588
BG = (32, 33, 37)
GUIDE = (70, 110, 130)
INK = (236, 227, 205)
GOLD = (224, 171, 78)
F = "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed-Bold.ttf"
FR = "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed.ttf"

POSES = [
    ("1-stoyka", "1 · СТОЙКА",
     "Опорный кадр. Колени согнуты, ноги на ширине шага, вес посередине, оружие выставлено на противника."),
    ("2-shag-perednej", "2 · ПЕРЕДНЯЯ ВПЕРЁД",
     "Передняя нога ушла вперёд на полшага, идёт низко над землёй. Ноги разведены шире всего, корпус в нижней точке, вес на задней."),
    ("3-podtyazhka", "3 · ЗАДНЯЯ ДОГНАЛА",
     "Задняя подтянулась к передней, ноги почти вместе, корпус в верхней точке. Руки и оружие неподвижны — работают только ноги."),
    ("4-perekat", "4 · ПЕРЕКАТ ВЕСА",
     "Задняя встала на землю, вес пошёл вперёд, передняя чуть разгружена, корпус подан на противника. Дальше снова кадр 1."),
    ("5-izgotovka", "5 · ИЗГОТОВКА",
     "Пауза перед броском: подсед, вес назад, замах. Самый сжатый кадр, из него вылетают в удар."),
    ("6-final", "6 · ФИНАЛ",
     "Поза из логотипа: рыцарь в выпаде с занесённым мечом, красный отвёрнут от удара."),
]

def char_ghost(path, target_h, flip):
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getbbox())
    im = im.resize((max(1, int(im.width * target_h / im.height)), target_h), Image.LANCZOS)
    if flip: im = im.transpose(Image.FLIP_LEFT_RIGHT)
    a = im.split()[3].point(lambda v: int(v * .22))
    im.putalpha(a)
    return im

def build():
    os.makedirs(OUT, exist_ok=True)
    red = char_ghost(os.path.join(CON, "raid-character-left-start.png"), 397, True)
    sand = char_ghost(os.path.join(CON, "raid-character-right-start.png"), 392, False)
    for who, ghost, gx in (("krasnyy", red, 245), ("rycar", sand, 468)):
        for code, name, hint in POSES:
            img = Image.new("RGB", (W, H), BG)
            d = ImageDraw.Draw(img)
            for y in (GROUND - ghost.height, GROUND - int(ghost.height * .55), GROUND):
                d.line([(40, y), (W - 40, y)], fill=GUIDE, width=1)
            d.line([(40, GROUND), (W - 40, GROUND)], fill=(150, 190, 210), width=3)
            for x in range(40, W - 39, 24):
                d.line([(x, GROUND + 6), (x, GROUND + 14)], fill=GUIDE, width=1)
            d.line([(gx, 120), (gx, GROUND + 30)], fill=(60, 80, 96), width=1)
            img.paste(ghost, (gx - ghost.width // 2, GROUND - ghost.height), ghost)

            f1 = ImageFont.truetype(F, 30); f2 = ImageFont.truetype(FR, 19); f3 = ImageFont.truetype(FR, 15)
            d.text((44, 44), name, font=f1, fill=GOLD)
            words = hint.split(); line = ""; y = 86
            for wd in words:
                t = (line + " " + wd).strip()
                if d.textlength(t, font=f2) > W - 120: d.text((44, y), line, font=f2, fill=INK); y += 26; line = wd
                else: line = t
            d.text((44, y), line, font=f2, fill=INK)
            d.text((44, GROUND + 60), "Линия земли — синяя. Ступни ставить ровно на неё.", font=f3, fill=(150, 190, 210))
            d.text((44, GROUND + 84), "Рост, таз и голову держать по серым направляющим.", font=f3, fill=(140, 150, 160))
            d.text((44, GROUND + 108), "Фон прозрачный или ровно #202125.", font=f3, fill=(140, 150, 160))
            d.text((44, GROUND + 132), "Вид строго сбоку, лицом к противнику. Передняя нога во всех кадрах одна и та же.", font=f3, fill=(140, 150, 160))
            d.text((44, H - 60), "%s · %s · холст 696×1044" % (who, code), font=f3, fill=(110, 115, 125))
            img.save(os.path.join(OUT, "%s_%s.png" % (who, code)))
    print("шаблонов:", len(POSES) * 2, "->", OUT)

if __name__ == "__main__":
    build()
