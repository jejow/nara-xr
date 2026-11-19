"""Render the NARA bilingual cast with Supertonic voice profiles."""

import argparse
from hashlib import sha256
import io
import json
from pathlib import Path
import wave

RESOURCES = Path(__file__).resolve().parents[1] / "NARA/Assets/FinalProject/Resources/Nara"
LANGUAGES = ("id", "en")
SAMPLE_RATE = 24000
STEPS = 8


def load_content():
    profiles = json.loads((RESOURCES / "AvatarCatalog.json").read_text(encoding="utf-8"))["entries"]
    lines = json.loads((RESOURCES / "OfflineScript.json").read_text(encoding="utf-8"))["lines"]
    return {item["id"]: item for item in profiles}, {line["key"]: line for line in lines}


def line_text(line, language, profile):
    return line[language].replace("{name}", profile["label"])


def voice_path(speaker, key, language):
    return RESOURCES / "Voices" / speaker / f"{key}_{language}.wav"


class CastSpeech:
    def __init__(self, model_path, threads=3):
        from supertonic import TTS

        self.tts = TTS(model="supertonic-3", model_dir=Path(model_path).resolve(),
                       auto_download=False, intra_op_num_threads=threads, inter_op_num_threads=1)
        self.presets = {name: self.tts.get_voice_style(name) for name in self.tts.voice_style_names}
        self.styles = {}

    def style(self, name):
        from supertonic.core import Style

        if name not in self.styles:
            voices = [self.presets[key] for key in name.split("+")]
            first, *rest = voices
            # Blend both style vectors of two same-gender presets for a fixed timbre.
            self.styles[name] = first if not rest else Style(
                (first.ttl + rest[0].ttl) * .5, (first.dp + rest[0].dp) * .5)
        return self.styles[name]

    def speak(self, profile, text, language):
        import numpy as np

        seed = int(sha256((profile["id"] + language + text).encode()).hexdigest()[:8], 16)
        np.random.seed(seed)
        audio, duration = self.tts.synthesize(
            text, voice_style=self.style(profile["voiceStyle"]), lang=language,
            speed=profile["voiceRate"], total_steps=STEPS,
        )
        length = round(self.tts.sample_rate * float(np.asarray(duration).item()))
        return wav_bytes(audio.reshape(-1)[:length], self.tts.sample_rate)

    def render_lines(self, profile, texts, language):
        import numpy as np
        from supertonic.core import Style

        if len(texts) == 1:
            return [self.speak(profile, texts[0], language)]
        style = self.style(profile["voiceStyle"])
        count = len(texts)
        batch_style = Style(np.repeat(style.ttl, count, axis=0), np.repeat(style.dp, count, axis=0))
        seed = int(sha256((profile["id"] + language + "\n".join(texts)).encode()).hexdigest()[:8], 16)
        np.random.seed(seed)
        audio, durations = self.tts.model(texts, batch_style, STEPS, profile["voiceRate"], language)
        return [
            wav_bytes(take[:round(self.tts.sample_rate * float(duration))], self.tts.sample_rate)
            for take, duration in zip(audio, durations, strict=True)
        ]


def wav_bytes(audio, source_rate):
    import numpy as np

    frames = source_rate // 50
    blocks = audio[:len(audio) // frames * frames].reshape(-1, frames)
    if not blocks.size or not np.isfinite(audio).all():
        raise ValueError("Unexpected audio from Supertonic.")
    rms = np.sqrt(np.mean(blocks * blocks, axis=1))
    active = np.flatnonzero(rms >= max(.0025, rms.max() * .045))
    if len(active) < 12:
        raise ValueError("Synthesis returned too little speech.")
    audio = audio[max(0, (active[0] - 4) * frames): (active[-1] + 6) * frames]
    # Smooth one sample before interpolation to remove the highest frequencies.
    audio = np.convolve(audio, [0.25, 0.5, 0.25], mode="same")
    target_frames = round(len(audio) * SAMPLE_RATE / source_rate)
    audio = np.interp(np.arange(target_frames) * source_rate / SAMPLE_RATE, np.arange(len(audio)), audio)
    gain = min(.112 / float(np.sqrt(np.mean(rms[active] ** 2))), .88 / np.max(np.abs(audio)))
    audio *= gain
    fade = SAMPLE_RATE // 125
    audio[:fade] *= np.linspace(0, 1, fade)
    audio[-fade:] *= np.linspace(1, 0, fade)
    output = io.BytesIO()
    with wave.open(output, "wb") as writer:
        writer.setparams((1, 2, SAMPLE_RATE, 0, "NONE", "not compressed"))
        writer.writeframes(np.round(audio * 32767).astype("<i2").tobytes())
    return output.getvalue()


def render_cast(engine, profiles, lines, resume):
    for speaker, profile in profiles.items():
        for language in LANGUAGES:
            jobs = [(voice_path(speaker, key, language), line_text(line, language, profile))
                    for key, line in lines.items()]
            jobs = [(path, text) for path, text in jobs if not resume or not path.is_file()]
            for start in range(0, len(jobs), 4):
                batch = jobs[start:start + 4]
                takes = engine.render_lines(profile, [text for _, text in batch], language)
                for (path, _), audio in zip(batch, takes, strict=True):
                    path.parent.mkdir(parents=True, exist_ok=True)
                    pending = path.with_suffix(".tmp")
                    pending.write_bytes(audio)
                    pending.replace(path)
        print(f"NARA_VOICE {speaker} {profile['label']}: {len(lines) * len(LANGUAGES)} clips", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path, help="Local Supertonic 3 ONNX model directory")
    parser.add_argument("--threads", type=int, default=3)
    parser.add_argument("--only", nargs="+")
    parser.add_argument("--resume", action="store_true")
    args = parser.parse_args()
    profiles, lines = load_content()
    if args.only:
        profiles = {speaker: profiles[speaker] for speaker in args.only}
    render_cast(CastSpeech(args.model, args.threads), profiles, lines, args.resume)


if __name__ == "__main__":
    main()
