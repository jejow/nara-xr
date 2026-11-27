"""Stream mono PCM and caption cues for a validated NARA dialogue line."""

import base64
from contextlib import closing
import io
import json
import math
from pathlib import Path
import re
from threading import Lock, Thread
import time
from urllib.parse import urlencode
from urllib.request import Request
from uuid import uuid4, UUID
import wave

from nara_cast_voices import SAMPLE_RATE, wav_bytes

MAX_FRAMES = SAMPLE_RATE * 60
PCM_PACKET = SAMPLE_RATE // 10 * 2
CARTESIA_HOST = "https://api.cartesia.ai"
CARTESIA_VERSION = "2026-08-14"
CARTESIA_SOCKET = "wss://api.cartesia.ai/tts/websocket"
PIPER_MODELS = {
    "id": "id_ID-news_tts-medium",
    "en-feminine": "en_US-amy-low",
    "en-masculine": "en_US-ryan-low",
}


def char_offset(text, index):
    # Unity indexes UTF-16 code units.
    return len(text[:index].encode("utf-16-le")) // 2


def gender_for(profile):
    return "feminine" if profile["voiceStyle"].startswith("F") else "masculine"


def read_pcm(wav):
    with wave.open(io.BytesIO(wav), "rb") as clip:
        if clip.getframerate() != SAMPLE_RATE or clip.getnchannels() != 1 or clip.getsampwidth() != 2:
            raise ValueError("NPC voice must use 24 kHz mono PCM.")
        return clip.readframes(clip.getnframes())


def segments(text, clauses=False):
    start = 0
    for pause in re.finditer(r"([.!?;,])\s+(?=[^\W\d_])", text):
        end = pause.start() + 1
        if pause[1] == "," and (not clauses or end - start < 31 or len(text) - end < 23):
            continue
        if end - start < 12:
            continue
        yield start, end
        start = pause.end()
    if start < len(text):
        yield start, len(text)


def local_events(text, render, *, clauses=False):
    frame = 0
    for start, end in segments(text, clauses=clauses):
        pcm = render(text[start:end])
        if not pcm:
            raise ValueError("No local voice samples.")
        yield {"type": "cue", "frame": frame, "chars": char_offset(text, start)}
        frame += len(pcm) // 2
        yield {"type": "cue", "frame": frame, "chars": char_offset(text, end)}
        yield pcm


class PiperSpeech:
    def __init__(self, model_dir):
        self.paths = {}
        if model_dir:
            for language, name in PIPER_MODELS.items():
                model = Path(model_dir) / f"{name}.onnx"
                if model.is_file() and model.with_suffix(".onnx.json").is_file():
                    self.paths[language] = model
        self.voices = {}
        self.lock = Lock()

    def warm(self):
        if "id" in self.paths:
            Thread(target=self.warm_indonesian, daemon=True).start()

    def warm_indonesian(self):
        try:
            with self.lock:
                voice = self.load_voice("id")
                list(voice.synthesize("Selamat pagi."))
        except Exception as error:
            print(f"NARA_PIPER_WARMUP {type(error).__name__}", flush=True)

    def load_voice(self, key):
        from piper import PiperVoice

        if key not in self.voices:
            self.voices[key] = PiperVoice.load(self.paths[key])
        return self.voices[key]

    def voice_for(self, profile, language):
        return language if language == "id" else f"en-{gender_for(profile)}"

    def events(self, profile, language, text, deadline):
        from piper import SynthesisConfig

        key = self.voice_for(profile, language)
        remaining = deadline - time.monotonic()
        if remaining <= 0 or not self.lock.acquire(timeout=remaining):
            raise TimeoutError()
        try:
            voice = self.load_voice(key)
            config = SynthesisConfig(length_scale=1 / profile["voiceRate"])

            def render(part):
                import numpy as np

                if time.monotonic() >= deadline:
                    raise TimeoutError()
                samples = [chunk.audio_float_array for chunk in voice.synthesize(part, config)]
                if not samples:
                    return b""
                return read_pcm(wav_bytes(np.concatenate(samples), voice.config.sample_rate))

            yield from local_events(text, render)
        finally:
            self.lock.release()


class CartesiaSpeech:
    def __init__(self, key, opener):
        self.key, self.opener = key, opener
        self.catalogs = {}
        self.lock = Lock()

    def select_voice(self, profile, language, deadline):
        if language not in self.catalogs:
            with self.lock:
                if language not in self.catalogs:
                    remaining = deadline - time.monotonic()
                    if remaining <= 0:
                        raise TimeoutError()
                    query = urlencode({"language": language, "limit": 100})
                    request = Request(f"{CARTESIA_HOST}/voices?{query}", headers={
                        "Authorization": "Bearer " + self.key,
                        "Cartesia-Version": CARTESIA_VERSION,
                    })
                    with self.opener.open(request, timeout=remaining) as result:
                        body = result.read(512_001)
                    if len(body) > 512_000:
                        raise ValueError("Voice catalog is too large.")
                    choices = {}
                    for voice in json.loads(body).get("data", []):
                        try:
                            key = str(UUID(voice["id"]))
                        except (KeyError, ValueError, TypeError, AttributeError):
                            continue
                        if voice.get("access") == "public" or voice.get("is_owner"):
                            choices[key] = voice.get("gender")
                    if not choices:
                        raise ValueError("No voice is available for this language.")
                    self.catalogs[language] = choices
        catalog = self.catalogs[language]
        gender = gender_for(profile)
        voices = sorted(key for key in catalog if catalog[key] == gender) or sorted(catalog)
        index = int(profile["id"].rsplit("_", 1)[-1]) - 1
        return voices[index % len(voices)]

    def events(self, profile, language, text, deadline):
        from websockets.exceptions import WebSocketException
        from websockets.sync.client import connect

        first_audio = min(deadline, time.monotonic() + 5.0)
        voice = self.select_voice(profile, language, first_audio)
        remaining = first_audio - time.monotonic()
        if remaining <= 0:
            raise TimeoutError()
        context = str(uuid4())
        request = {
            "context_id": context,
            "model_id": "sonic-3.6",
            "transcript": text,
            "language": language,
            "voice": voice,
            "output_format": {"container": "raw", "encoding": "pcm_s16le", "sample_rate": SAMPLE_RATE},
            "add_timestamps": True,
            "continue": False,
        }
        position = 0
        next_frame = 0
        clock = first_audio
        try:
            with connect(f"{CARTESIA_SOCKET}?cartesia_version={CARTESIA_VERSION}",
                         additional_headers={"X-API-Key": self.key},
                         open_timeout=remaining, close_timeout=.4, max_size=512_000) as socket:
                socket.send(json.dumps(request))
                while True:
                    remaining = clock - time.monotonic()
                    if remaining <= 0:
                        raise TimeoutError()
                    message = json.loads(socket.recv(timeout=remaining))
                    if message.get("type") == "error":
                        raise ConnectionError("Voice service rejected synthesis.")
                    if message.get("context_id") != context:
                        continue
                    kind = message.get("type")
                    if kind == "chunk":
                        pcm = base64.b64decode(message["data"], validate=True)
                        if pcm:
                            clock = deadline
                            yield pcm
                    elif kind == "timestamps":
                        timing = message.get("word_timestamps", {})
                        for word, start in zip(timing.get("words", []), timing.get("start", [])):
                            if not isinstance(word, str) or not isinstance(start, (int, float)):
                                continue
                            if not math.isfinite(start) or not 0 <= start < MAX_FRAMES / SAMPLE_RATE:
                                continue
                            match = re.search(re.escape(word), text[position:], re.IGNORECASE)
                            if match is None:
                                continue
                            offset = position + match.start()
                            position += match.end()
                            frame = round(start * SAMPLE_RATE)
                            if frame >= next_frame:
                                next_frame = frame
                                yield {"type": "cue", "frame": frame, "chars": char_offset(text, offset)}
                    elif kind == "done":
                        return
        except WebSocketException as error:
            raise ConnectionError("Voice stream disconnected.") from error


class StreamSpeech:
    def __init__(self, piper_dir, cartesia_key, cartesia_key_file, opener, matched_voice):
        self.piper = PiperSpeech(piper_dir)
        self.opener = opener
        self.cartesia_key_file = Path(cartesia_key_file) if cartesia_key_file and not cartesia_key else None
        self.cartesia_timestamp = None
        self.cartesia_lock = Lock()
        self.cartesia_voice = CartesiaSpeech(cartesia_key, opener) if cartesia_key else None
        self.matched_voice = matched_voice

    def configured_cartesia(self):
        if self.cartesia_key_file is None:
            return self.cartesia_voice
        with self.cartesia_lock:
            try:
                taken = self.cartesia_key_file.stat().st_mtime_ns
            except OSError:
                taken = -1
            if taken == self.cartesia_timestamp:
                return self.cartesia_voice
            self.cartesia_timestamp = taken
            self.cartesia_voice = None
            if taken != -1:
                try:
                    import win32crypt

                    encrypted = bytes.fromhex(self.cartesia_key_file.read_text(encoding="ascii").strip())
                    key = win32crypt.CryptUnprotectData(encrypted, None, None, None, 0)[1].decode("utf-16-le").strip()
                    if key.startswith("sk_car_"):
                        self.cartesia_voice = CartesiaSpeech(key, self.opener)
                except Exception as error:
                    print(f"NARA_CARTESIA_CONFIG {type(error).__name__}", flush=True)
            return self.cartesia_voice

    @property
    def available(self):
        return bool(self.piper.paths or self.matched_voice or self.configured_cartesia())

    @property
    def preferred(self):
        if self.configured_cartesia():
            return "cartesia"
        if self.piper.paths:
            return "piper"
        return "supertonic" if self.matched_voice else ""

    def events(self, profile, language, text, deadline):
        providers = []
        cartesia = self.configured_cartesia()
        if cartesia:
            providers.append(("cartesia", cartesia.events(profile, language, text, deadline)))
        if self.piper.voice_for(profile, language) in self.piper.paths:
            providers.append(("piper", self.piper.events(profile, language, text, deadline)))
        if self.matched_voice:
            providers.append(("supertonic", local_events(
                text, lambda part: read_pcm(self.matched_voice(profile["id"], language, part, deadline)),
                clauses=True)))
        for name, source in providers:
            frames = 0
            cues = []
            started = time.monotonic()
            try:
                with closing(source):
                    for packet in source:
                        if isinstance(packet, dict):
                            if frames:
                                yield packet
                            elif len(cues) < 420:
                                cues.append(packet)
                            continue
                        if len(packet) % 2 or frames + len(packet) // 2 > MAX_FRAMES:
                            raise ValueError("Unexpected PCM length.")
                        if not frames:
                            yield {"type": "start", "sampleRate": SAMPLE_RATE, "provider": name}
                            yield from cues
                            print(f"NARA_AUDIO_FIRST {name} {time.monotonic() - started:.2f}s", flush=True)
                        for offset in range(0, len(packet), PCM_PACKET):
                            yield {"type": "audio", "data": base64.b64encode(packet[offset:offset + PCM_PACKET]).decode("ascii")}
                        frames += len(packet) // 2
                if frames:
                    yield {"type": "end", "frame": frames, "chars": char_offset(text, len(text))}
                    return
            except Exception as error:
                if isinstance(error, OSError) and hasattr(error, "close"):
                    error.close()
                print(f"NARA_AUDIO_FALLBACK {name} {type(error).__name__}", flush=True)
                if frames:
                    break
        yield {"type": "error"}
