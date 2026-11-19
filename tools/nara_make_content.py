"""Generate the example slide deck and locally synthesized offline lines."""

import argparse
import ctypes
import json
import os
from pathlib import Path
import wave


DESTINATION = Path(__file__).resolve().parents[1] / "NARA/Assets/FinalProject/Resources/Nara"


def make_slides():
    from PIL import Image, ImageDraw, ImageFont

    fonts = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    paper, ink, teal, mint, soft = "#f6f5eb", "#10292b", "#235b4f", "#d4e7d7", "#91a69d"

    def font(size, bold=False):
        family = fonts / ("segoeuib.ttf" if bold else "segoeui.ttf")
        return ImageFont.truetype(str(family) if family.exists() else ("DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"), size)

    slides = [
        ("MASALAH", "Ruang belajar\nkampus", "Mahasiswa perlu menemukan ruang\ndiskusi kosong sebelum berangkat.", [
            ("Kebutuhan", "Tahu ruang yang tersedia."),
            ("Pengguna", "Kelompok belajar kecil."),
        ]),
        ("USULAN", "Peminjaman\nsatu alur", "Tampilkan slot yang tersedia\ndan konfirmasi pemesanan.", [
            ("Cari ruang", "Lihat tempat dan waktu."),
            ("Pilih slot", "Periksa kapasitasnya."),
            ("Konfirmasi", "Simpan jadwal diskusi."),
        ]),
        ("PENGUJIAN", "Apakah lebih\nmudah?", "Bandingkan cara lama dan prototipe\ndengan tugas pencarian yang sama.", [
            ("Waktu", "Durasi menemukan ruang."),
            ("Keberhasilan", "Slot sesuai kebutuhan."),
        ]),
    ]
    folder = DESTINATION / "Slides"
    folder.mkdir(parents=True, exist_ok=True)
    for index, (label, title, description, boxes) in enumerate(slides, 1):
        img = Image.new("RGB", (1600, 900), paper)
        draw = ImageDraw.Draw(img)
        draw.rectangle((0, 0, 20, 900), fill=teal)
        draw.text((92, 82), label, font=font(28, True), fill=teal)
        draw.multiline_text((86, 169), title, font=font(100, True), fill=ink, spacing=-2)
        draw.multiline_text((92, 455), description, font=font(38), fill=ink, spacing=13)
        draw.rounded_rectangle((890, 130, 1515, 748), radius=42, fill=ink)
        for k, (name, detail) in enumerate(boxes):
            top = 179 + k * 174
            draw.rounded_rectangle((930, top, 1474, top + 140), radius=21, fill=mint)
            draw.text((965, top + 24), name, font=font(39, True), fill=ink)
            draw.text((965, top + 80), detail, font=font(31), fill=teal)
        draw.line((90, 801, 1515, 801), fill=soft, width=2)
        draw.text((91, 821), "NARA  /  Materi contoh", font=font(27), fill=teal)
        img.save(folder / f"slide_{index}.png", optimize=True)


def make_voice():
    from espeakng_loader import get_data_path, load_library

    engine = load_library()
    engine.espeak_Initialize.argtypes = [
        ctypes.c_int, ctypes.c_int, ctypes.c_char_p, ctypes.c_int
    ]
    engine.espeak_SetVoiceByName.argtypes = [ctypes.c_char_p]
    engine.espeak_SetParameter.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int]
    engine.espeak_Synth.argtypes = [
        ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_int,
        ctypes.c_uint, ctypes.c_uint, ctypes.c_void_p, ctypes.c_void_p,
    ]
    collect_audio = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(ctypes.c_int16), ctypes.c_int, ctypes.c_void_p)
    engine.espeak_SetSynthCallback.argtypes = [collect_audio]
    rate = engine.espeak_Initialize(2, 0, os.fsencode(str(Path(get_data_path()).parent)), 0)
    if rate <= 0:
        raise RuntimeError("eSpeak NG initialization failed")

    pcm = bytearray()

    @collect_audio
    def append_audio(samples, count, _events):
        if samples and count:
            pcm.extend(ctypes.string_at(samples, count * 2))
        return 0

    engine.espeak_SetSynthCallback(append_audio)
    engine.espeak_SetParameter(1, 154, 0)
    engine.espeak_SetParameter(3, 49, 0)
    data = json.loads((DESTINATION / "OfflineScript.json").read_text(encoding="utf-8"))
    for language, voice in (("id", b"id+f3"), ("en", b"en-us+f3")):
        if engine.espeak_SetVoiceByName(voice) != 0:
            raise RuntimeError(f"Voice unavailable for {language}")
        for line in data["lines"]:
            if line["key"] == "intro":
                continue
            pcm.clear()
            text = line[language].encode("utf-8")
            status = engine.espeak_Synth(text, len(text) + 1, 0, 1, 0, 1 | 0x1000, None, None)
            if status != 0 or len(pcm) < rate:
                raise RuntimeError("Offline audio could not be generated")
            name = f"{line['key']}_{language}.wav"
            with wave.open(str(DESTINATION / name), "wb") as f:
                f.setnchannels(1)
                f.setsampwidth(2)
                f.setframerate(rate)
                f.writeframes(pcm)
            print(f"{name}  {len(pcm) / (2 * rate):.2f} s")
    engine.espeak_Terminate()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--slides-only", action="store_true")
    options = parser.parse_args()
    make_slides()
    if not options.slides_only:
        make_voice()
