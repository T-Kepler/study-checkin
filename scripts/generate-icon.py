from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "windows" / "StudyCheckin.Desktop" / "Assets"
PNG_PATH = ASSETS / "app-icon.png"
ICO_PATH = ASSETS / "app-icon.ico"

INK = "#1C2B33"
PAPER = "#F4F6F5"
TEAL = "#17877F"
GOLD = "#C6902B"


def create_icon() -> Image.Image:
    image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((32, 32, 992, 992), radius=200, fill=INK)

    draw.polygon(
        [(176, 280), (448, 330), (512, 390), (512, 760), (430, 700), (176, 648)],
        fill=PAPER,
    )
    draw.polygon(
        [(848, 280), (576, 330), (512, 390), (512, 760), (594, 700), (848, 648)],
        fill=PAPER,
    )
    draw.polygon(
        [(716, 258), (796, 258), (796, 427), (756, 394), (716, 427)],
        fill=GOLD,
    )

    check_points = [(308, 497), (456, 645), (721, 380)]
    draw.line(check_points, fill=TEAL, width=88, joint="curve")
    radius = 44
    for x, y in (check_points[0], check_points[-1]):
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=TEAL)

    return image


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    image = create_icon()
    image.save(PNG_PATH, "PNG", optimize=True)
    image.save(
        ICO_PATH,
        "ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    print(f"Generated: {PNG_PATH}")
    print(f"Generated: {ICO_PATH}")


if __name__ == "__main__":
    main()
