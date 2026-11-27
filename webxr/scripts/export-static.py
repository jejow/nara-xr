"""Copy and compress approved NARA presentation assets for browser use."""
from pathlib import Path
import json
import shutil

from PIL import Image
import soundfile as sf


ROOT = Path(__file__).resolve().parents[2]
FINAL = ROOT / "NARA/Assets/FinalProject"
SOURCE = FINAL / "Resources/Nara"
DESTINATION = ROOT / "webxr/public/media"


def copy(origin, name):
    target = DESTINATION / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(origin, target)


for asset in ("AvatarCatalog.json", "OfflineScript.json", "SeatedMotion.bytes", "OverteMotion.bytes"):
    copy(SOURCE / asset, asset)
# Include local HY-Motion takes when available under the upstream license in the original project.
if (SOURCE / "LocalAudienceRaises.bytes").is_file():
    copy(SOURCE / "LocalAudienceRaises.bytes", "LocalAudienceRaises.bytes")
for asset in ("Inter-Regular.ttf", "Inter-Bold.ttf", "Inter-OFL.txt"):
    copy(SOURCE / "Fonts" / asset, f"fonts/{asset}")
for asset in ["Overte-Apache-2.0.txt", "Overte-NOTICE.txt"]:
    copy(FINAL.parent / "StreamingAssets/ThirdParty" / asset, f"credits/{asset}")
copy(FINAL / "Characters/Rocketbox/LICENSE-Microsoft-Rocketbox.md", "credits/Microsoft-Rocketbox-MIT.md")
copy(ROOT / "docs/ASSETS.md", "credits/ASSETS.md")
copy(ROOT / "docs/SUARA-KARAKTER.md", "credits/SUARA-KARAKTER.md")

slides = DESTINATION / "slides"
slides.mkdir(parents=True, exist_ok=True)
for path in (SOURCE / "Slides").glob("slide_*.png"):
    with Image.open(path) as source:
        image = source.convert("RGB")
        image.thumbnail((1440, 900))
        image.save(slides / (path.stem + ".jpg"), quality=89, optimize=True)
for voice in (SOURCE / "Voices").glob("*/*.wav"):
    target = DESTINATION / "voices" / voice.parent.name / (voice.stem + ".ogg")
    target.parent.mkdir(parents=True, exist_ok=True)
    samples, rate = sf.read(voice, dtype="float32")
    sf.write(target, samples, rate, format="OGG", subtype="VORBIS", compression_level=.66)

catalog = json.loads((SOURCE / "AvatarCatalog.json").read_text(encoding="utf-8-sig"))["entries"]
print(f"Exported {len(catalog)} voice profiles and {len(list(slides.glob('*.jpg')))} sample slides.")
