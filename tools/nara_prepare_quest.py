"""Build the Java and native dependencies for the NARA Quest APK."""

import argparse
import ast
import hashlib
import os
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "quest"
UNITY_PROJECT = ROOT / "NARA"
PLUGIN = UNITY_PROJECT / "Assets" / "Plugins" / "Android"
MAVEN = "https://repo.maven.apache.org/maven2/"
SHERPA = "https://github.com/k2-fsa/sherpa-onnx/releases/download/"
TESSDATA = "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/4.1.0/"

DEPENDENCIES = {
    "nanohttpd-2.3.1.jar": (MAVEN + "org/nanohttpd/nanohttpd/2.3.1/nanohttpd-2.3.1.jar",
                            "de864c47818157141a24c9acb36df0c47d7bf15b7ff48c90610f3eb4e5df0e58"),
    "kotlin-stdlib-2.2.21.jar": (MAVEN + "org/jetbrains/kotlin/kotlin-stdlib/2.2.21/kotlin-stdlib-2.2.21.jar",
                                 "6558a3d233da56a20934b32159f9db5f86ed5816ef098f78a2c223dc6abb79dd"),
    "poi-5.4.1.jar": (MAVEN + "org/apache/poi/poi/5.4.1/poi-5.4.1.jar",
                       "da5abf42da4604c5a7bca38956af6e9d6f196d9b6d4cb7eabee4f480b580d505"),
    "commons-io-2.18.0.jar": (MAVEN + "commons-io/commons-io/2.18.0/commons-io-2.18.0.jar",
                               "f3ca0f8d63c40e23a56d54101c60d5edee136b42d84bfb85bc7963093109cf8b"),
    "log4j-api-2.24.3.jar": (MAVEN + "org/apache/logging/log4j/log4j-api/2.24.3/log4j-api-2.24.3.jar",
                              "5b4a0a0cd0e751ded431c162442bdbdd53328d1f8bb2bae5fc1bbeee0f66d80f"),
    "pdfbox-android-2.0.27.0.aar": (MAVEN + "com/tom-roush/pdfbox-android/2.0.27.0/pdfbox-android-2.0.27.0.aar",
                                   "30277f879cfd571db2a137582c95516a0d4ea6778e945519bc58ca93d57d88c7"),
    # Piper models use the internal eSpeak frontend, which was removed in Sherpa-ONNX 2.0.
    "sherpa-onnx-1.13.8.aar": (SHERPA + "v1.13.8/sherpa-onnx-static-link-onnxruntime-1.13.8.aar",
                               "b22c3fc1b6a45666d28892bb2f7694beeb77a8362d7ebd77c1a5431ec9435471"),
    "tesseract4android-4.9.0.aar": ("https://jitpack.io/cz/adaptech/tesseract4android/tesseract4android/4.9.0/"
                                   "tesseract4android-4.9.0.aar",
                                   "bce5d6413a1a5ae3d7240033fbbc851ba3217d0a08d9769400e17a077f42cb2a"),
    "asr-tiny.tar.bz2": (SHERPA + "asr-models/sherpa-onnx-whisper-tiny.tar.bz2",
                        "c46116994e539aa165266d96b325252728429c12535eb9d8b6a2b10f129e66b1"),
    "voice-id.tar.bz2": (SHERPA + "tts-models/vits-piper-id_ID-news_tts-medium.tar.bz2",
                         "634b0169fe41c368e9fafe128f30647f9e0301e852ce503cd78146db524f1f12"),
    "voice-amy.tar.bz2": (SHERPA + "tts-models/vits-piper-en_US-amy-medium.tar.bz2",
                          "c70f5284a09a7fd4ed203b39b2ff51cac1432b422b852eb647b481dade3cf639"),
    "voice-ryan.tar.bz2": (SHERPA + "tts-models/vits-piper-en_US-ryan-medium.tar.bz2",
                           "08d0522884652402b32d6995d9df776e85c7451a333fabe97e9e4635faf568ac"),
    "ind.traineddata": (TESSDATA + "ind.traineddata",
                         "69786901da87ab8766c1ea7fbb10b28f2110c14da3f6c8f2735df131fba95d88"),
    "eng.traineddata": (TESSDATA + "eng.traineddata",
                         "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"),
}

AARS = {
    "sherpa-onnx-1.13.8.aar": "NaraSherpa.aar",
    "pdfbox-android-2.0.27.0.aar": "NaraPdfBox.aar",
    "tesseract4android-4.9.0.aar": "NaraTesseract.aar",
}
ASR_FILES = {"tiny-encoder.int8.onnx", "tiny-decoder.int8.onnx", "tiny-tokens.txt"}


def dependency(cache, name, url, checksum, offline):
    file = cache / name
    if not file.exists():
        if offline:
            raise FileNotFoundError(file)
        print(f"Downloading {name}", flush=True)
        part = file.with_suffix(file.suffix + ".partial")
        try:
            with urllib.request.urlopen(url, timeout=60) as response, part.open("wb") as output:
                shutil.copyfileobj(response, output)
            verify(part, checksum)
            part.replace(file)
        finally:
            part.unlink(missing_ok=True)
    else:
        verify(file, checksum)
    return file


def verify(file, expected):
    with file.open("rb") as stream:
        actual = hashlib.file_digest(stream, "sha256").hexdigest()
    if actual != expected:
        raise ValueError(f"SHA-256 mismatch for {file.name}")


def zip_member(source, name, archive, target):
    with source.open(name) as incoming, archive.open(target, "w") as output:
        shutil.copyfileobj(incoming, output)


def write_models(archive, file, voice):
    selected = set()
    with tarfile.open(file, "r|bz2") as model:
        for item in model:
            if not item.isfile():
                continue
            relative = PurePosixPath(*PurePosixPath(item.name).parts[1:])
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError(f"Invalid model path in {file.name}")
            prefix = "assets/nara-speech/"
            if voice == "asr":
                if relative.name not in ASR_FILES:
                    continue
                name = "asr/" + relative.name
            elif relative.name.endswith(".onnx"):
                name = voice + "/voice.onnx"
            elif relative.name == "tokens.txt":
                name = voice + "/tokens.txt"
            elif relative.name.upper() in {"LICENSE", "MODEL_CARD", "COPYING"}:
                name = voice + "/" + relative.name
            elif voice == "id" and relative.parts[:1] == ("espeak-ng-data",):
                name = relative.as_posix()
            else:
                continue
            if name in selected:
                raise ValueError(f"Duplicate model file {name}")
            selected.add(name)
            with model.extractfile(item) as source, archive.open(prefix + name, "w") as target:
                shutil.copyfileobj(source, target)
    required = {f"asr/{file}" for file in ASR_FILES} if voice == "asr" else {
        f"{voice}/voice.onnx", f"{voice}/tokens.txt"
    }
    if required - selected or voice == "id" and "espeak-ng-data/phondata" not in selected:
        raise ValueError(f"Incomplete models for {voice}")


def build_classes(javac, android, files, temporary):
    classes = temporary / "classes"
    classes.mkdir()
    dependencies = [android] + [value for name, value in files.items() if name.endswith(".jar")]
    for name in AARS:
        jar = temporary / (name + ".jar")
        with zipfile.ZipFile(files[name]) as contents:
            jar.write_bytes(contents.read("classes.jar"))
        dependencies.append(jar)
    java = sorted(SOURCE.rglob("*.java"))
    subprocess.run([str(javac), "-encoding", "UTF-8", "--release", "8",
                    "-classpath", os.pathsep.join(map(str, dependencies)),
                    "-d", str(classes), *map(str, java)], check=True)
    output = temporary / "classes.jar"
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(classes.rglob("*.class")):
            archive.write(path, path.relative_to(classes).as_posix())
    return output


def write_android_dependencies(package, files, temporary):
    runtime = temporary / "nara-runtime.jar"
    entries = set()
    with zipfile.ZipFile(runtime, "w", zipfile.ZIP_DEFLATED) as output:
        for name, path in files.items():
            if not name.endswith(".jar"):
                continue
            with zipfile.ZipFile(path) as library:
                for item in library.infolist():
                    relative = item.filename
                    if (item.is_dir() or relative in {"module-info.class", "META-INF/MANIFEST.MF"}
                            or relative.startswith("META-INF/versions/")):
                        continue
                    if relative.startswith(("META-INF/LICENSE", "META-INF/NOTICE", "META-INF/DEPENDENCIES")):
                        zip_member(library, item, package, f"assets/nara-licenses/{name}/{PurePosixPath(relative).name}")
                        continue
                    if relative in entries:
                        raise ValueError(f"Duplicate Android runtime entry {relative} in {name}")
                    entries.add(relative)
                    zip_member(library, item, output, relative)
    package.write(runtime, "libs/nara-runtime.jar")


def refresh_prompt():
    source = ROOT / "tools" / "nara_voice_server.py"
    tree = ast.parse(source.read_text(encoding="utf-8"))
    prompt = next(node.value.value for node in tree.body if isinstance(node, ast.Assign)
                  and any(isinstance(x, ast.Name) and x.id == "SYSTEM" for x in node.targets))
    file = UNITY_PROJECT / "Assets/FinalProject/Resources/Nara/DialoguePrompt.txt"
    if not file.exists() or file.read_text(encoding="utf-8") != prompt + "\n":
        file.write_text(prompt + "\n", encoding="utf-8")


def prepare(args):
    args.cache.mkdir(parents=True, exist_ok=True)
    files = {name: dependency(args.cache, name, *details, args.offline) for name, details in DEPENDENCIES.items()}
    PLUGIN.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="nara-quest-", dir=args.cache) as directory:
        temporary = Path(directory)
        classes = build_classes(args.javac, args.android, files, temporary)
        path = temporary / "NaraStandalone.aar"
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=1) as package:
            package.write(SOURCE / "AndroidManifest.xml", "AndroidManifest.xml")
            package.write(SOURCE / "consumer-rules.pro", "proguard.txt")
            package.write(SOURCE / "res/xml/nara_network_security.xml", "res/xml/nara_network_security.xml")
            package.write(classes, "classes.jar")
            write_android_dependencies(package, files, temporary)
            for name in files:
                if name.endswith(".traineddata"):
                    package.write(files[name], "assets/nara-ocr/" + name)
            for name, voice in [("asr-tiny", "asr"), ("voice-id", "id"),
                                ("voice-amy", "en-amy"), ("voice-ryan", "en-ryan")]:
                print(f"Packaging {voice}", flush=True)
                write_models(package, files[name + ".tar.bz2"], voice)
        for name, target in AARS.items():
            with zipfile.ZipFile(files[name]) as source, zipfile.ZipFile(
                    temporary / target, "w", zipfile.ZIP_DEFLATED, compresslevel=1) as output:
                for info in source.infolist():
                    path_parts = PurePosixPath(info.filename).parts
                    if path_parts[:1] == ("jni",) and path_parts[1:2] != ("arm64-v8a",):
                        continue
                    if not info.is_dir():
                        zip_member(source, info, output, info.filename)
        for filename in [*AARS.values(), "NaraStandalone.aar"]:
            shutil.move(temporary / filename, PLUGIN / filename)
    refresh_prompt()
    print(f"NARA Quest libraries: {PLUGIN}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", required=True, type=Path)
    parser.add_argument("--javac", required=True, type=Path)
    parser.add_argument("--android", required=True, type=Path, help="Path to the Android SDK platform android.jar")
    parser.add_argument("--offline", action="store_true", help="Use only verified files already in the dependency cache")
    prepare(parser.parse_args())


if __name__ == "__main__":
    main()
