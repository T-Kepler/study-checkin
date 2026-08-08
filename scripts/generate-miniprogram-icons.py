from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "src" / "miniprogram" / "assets" / "tabs"
SIZE = 81
MUTED = "#607078"
TEAL = "#176B68"


def canvas():
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image)


def draw_today(color):
    image, draw = canvas()
    draw.rounded_rectangle((15, 14, 66, 67), radius=12, outline=color, width=5)
    draw.line([(25, 42), (36, 53), (57, 30)], fill=color, width=6, joint="curve")
    return image


def draw_history(color):
    image, draw = canvas()
    draw.ellipse((14, 14, 67, 67), outline=color, width=5)
    draw.line([(40, 25), (40, 43), (52, 50)], fill=color, width=5, joint="curve")
    return image


def draw_statistics(color):
    image, draw = canvas()
    draw.line([(14, 66), (67, 66)], fill=color, width=5)
    draw.rounded_rectangle((19, 43, 29, 62), radius=2, fill=color)
    draw.rounded_rectangle((36, 31, 46, 62), radius=2, fill=color)
    draw.rounded_rectangle((53, 18, 63, 62), radius=2, fill=color)
    return image


def draw_settings(color):
    image, draw = canvas()
    for y, knob_x in ((24, 30), (41, 52), (58, 37)):
        draw.line([(16, y), (65, y)], fill=color, width=5)
        draw.ellipse((knob_x - 6, y - 6, knob_x + 6, y + 6), fill=color)
    return image


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    icons = {
        "today": draw_today,
        "history": draw_history,
        "statistics": draw_statistics,
        "settings": draw_settings,
    }
    for name, draw_icon in icons.items():
        draw_icon(MUTED).save(OUTPUT / f"{name}.png", optimize=True)
        draw_icon(TEAL).save(OUTPUT / f"{name}-active.png", optimize=True)
    print(f"Generated 8 tab icons in {OUTPUT}")


if __name__ == "__main__":
    main()
