"""Convert a local presentation to slide pictures and extracted text for NARA."""

from contextlib import closing
import io
import json
from pathlib import Path
import sys
import unicodedata
from zipfile import ZipFile

MAX_UPLOAD = 32_000_000
MAX_PACKAGE = 32_000_000
MAX_PAGES = 60
MAX_SIDE = 1600
FORMATS = {".pptx", ".ppt", ".pdf", ".png", ".jpg", ".jpeg"}
TESSDATA = Path.home() / ".nara" / "relay" / "models" / "tessdata"


class DeckLimit(ValueError):
    pass


def office_pdf(source, output):
    import win32com.client

    app = win32com.client.Dispatch("PowerPoint.Application")
    previous_security = app.AutomationSecurity
    try:
        # Office opens a private copy without a window, with macros disabled.
        app.AutomationSecurity = 3  # msoAutomationSecurityForceDisable
        deck = app.Presentations.Open(str(source), -1, 0, 0)
    finally:
        app.AutomationSecurity = previous_security
    try:
        if not 1 <= deck.Slides.Count <= MAX_PAGES:
            raise DeckLimit()
        deck.SaveCopyAs(str(output), 32)  # ppSaveAsPDF
    finally:
        deck.Close()


def verify_source(path):
    with path.open("rb") as stream:
        header = stream.read(1024)
    suffix = path.suffix.lower()
    if suffix == ".pptx":
        with ZipFile(path) as archive:
            entries = archive.infolist()
            if len(entries) > 8000 or sum(entry.file_size for entry in entries) > 256_000_000:
                raise DeckLimit()
            if "ppt/presentation.xml" not in archive.namelist():
                raise ValueError("Wrong PowerPoint container.")
    elif suffix == ".ppt" and not header.startswith(bytes.fromhex("D0CF11E0A1B11AE1")):
        raise ValueError("Wrong PowerPoint header.")
    elif suffix == ".pdf" and b"%PDF-" not in header:
        raise ValueError("Wrong PDF header.")


def clean_text(text):
    lines = []
    for line in unicodedata.normalize("NFKC", text).splitlines():
        line = " ".join("".join(char for char in line if char.isprintable() or char.isspace()).split())
        if line and (not lines or line != lines[-1]):
            lines.append(line)
    return "\n".join(lines)[:2500]


def ocr_text(image):
    from tesserocr import PSM, PyTessBaseAPI

    if not all((TESSDATA / f"{code}.traineddata").is_file() for code in ("ind", "eng")):
        return ""
    with PyTessBaseAPI(path=str(TESSDATA), lang="ind+eng", psm=PSM.SPARSE_TEXT) as reader:
        reader.SetImage(image)
        text = reader.GetUTF8Text()
        return clean_text(text) if reader.MeanTextConf() >= 50 else ""


def write_page(output, pages, image, text=""):
    from PIL import Image

    image.thumbnail((MAX_SIDE, MAX_SIDE), Image.Resampling.LANCZOS)
    if "A" in image.getbands():
        rgb = Image.new("RGB", image.size, "white")
        rgb.paste(image, mask=image.getchannel("A"))
    else:
        rgb = image.convert("RGB")
    text = clean_text(text)
    if len(text) < 25:
        recognized = ocr_text(rgb)
        if len(recognized) > len(text):
            text = recognized
    number = len(pages) + 1
    picture = io.BytesIO()
    rgb.save(picture, "JPEG", quality=87, optimize=True)
    output.writestr(f"slide-{number:03}.jpg", picture.getvalue())
    pages.append({"page": number, "text": text})


def pdf_pages(path, output, pages):
    import pypdfium2

    with closing(pypdfium2.PdfDocument(path)) as pdf:
        if not 1 <= len(pdf) <= MAX_PAGES:
            raise DeckLimit()
        for index in range(len(pdf)):
            with closing(pdf[index]) as page:
                with closing(page.get_textpage()) as text_page:
                    text = text_page.get_text_bounded()
                longest = max(page.get_size())
                if not 1 <= longest <= 14000:
                    raise ValueError("Unexpected PDF page size.")
                with closing(page.render(scale=MAX_SIDE / longest)) as bitmap:
                    write_page(output, pages, bitmap.to_pil(), text)


def build_deck(source, destination):
    from PIL import Image, ImageOps

    suffix = source.suffix.lower()
    if suffix not in FORMATS or not 0 < source.stat().st_size <= MAX_UPLOAD:
        raise DeckLimit()
    verify_source(source)
    pages = []
    with ZipFile(destination, "w") as output:
        if suffix in {".pptx", ".ppt", ".pdf"}:
            pdf = source
            if suffix in {".pptx", ".ppt"}:
                pdf = destination.with_suffix(".pdf")
                office_pdf(source, pdf)
            pdf_pages(pdf, output, pages)
        else:
            with Image.open(source) as image:
                if image.format not in {"PNG", "JPEG"} or image.width * image.height > 36_000_000:
                    raise ValueError("Unexpected image format or size.")
                write_page(output, pages, ImageOps.exif_transpose(image).convert("RGBA"))
        title = next(iter(pages[0]["text"].splitlines()), "")[:100]
        output.writestr("deck.json", json.dumps({"version": 1, "topic": title, "pages": pages}, ensure_ascii=False))
    if destination.stat().st_size > MAX_PACKAGE:
        raise DeckLimit()


if __name__ == "__main__":
    try:
        build_deck(*(Path(arg).resolve() for arg in sys.argv[1:]))
    except DeckLimit:
        raise SystemExit(2)
    except ImportError:
        raise SystemExit(3)
    except Exception as error:
        print(type(error).__name__, file=sys.stderr)
        raise SystemExit(4)
