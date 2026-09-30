"""Build the Android toast DEX and JNI bridge for embedding in Utility.dll."""

import argparse
import os
import subprocess
import tempfile
from pathlib import Path
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "android" / "build"
TOOLS = ROOT / ".tools"


def download(url: str, path: Path) -> Path:
    if not path.exists():
        TOOLS.mkdir(exist_ok=True)
        temporary = path.with_suffix(".download")
        with urlopen(url, timeout=60) as response, temporary.open("wb") as output:
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
        temporary.replace(path)
    return path


def run(*args: str) -> None:
    subprocess.run(args, cwd=ROOT, check=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--android-jar", type=Path)
    parser.add_argument("--d8-jar", type=Path)
    parser.add_argument("--ndk", type=Path, default=os.environ.get("ANDROID_NDK_HOME"))
    args = parser.parse_args()
    sdk = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
    android = args.android_jar or (
        Path(sdk) / "platforms" / "android-35" / "android.jar" if sdk else None
    )
    if android is None or not android.exists():
        parser.error("--android-jar or an Android SDK with platform android-35 is required")
    d8 = args.d8_jar or download(
        "https://dl.google.com/dl/android/maven2/com/android/tools/r8/8.3.37/r8-8.3.37.jar",
        TOOLS / "r8-8.3.37.jar",
    )
    if args.ndk is None:
        parser.error("--ndk or ANDROID_NDK_HOME is required")
    toolchain = args.ndk / "toolchains" / "llvm" / "prebuilt" / "windows-x86_64" / "bin"
    if not toolchain.exists():
        toolchain = args.ndk / "toolchains" / "llvm" / "prebuilt" / "linux-x86_64" / "bin"
    compiler = toolchain / ("clang.exe" if os.name == "nt" else "clang")
    if not compiler.exists():
        parser.error(f"NDK clang is missing: {compiler}")

    OUT.mkdir(parents=True, exist_ok=True)
    sources = sorted((ROOT / "android" / "src").rglob("*.java"))
    with tempfile.TemporaryDirectory(prefix="utility-toast-") as temporary:
        classes = Path(temporary)
        run("javac", "--release", "8", "-encoding", "UTF-8", "-cp", str(android),
            "-d", str(classes), *(str(path) for path in sources))
        run("java", "-cp", str(d8), "com.android.tools.r8.D8", "--min-api", "26",
            "--lib", str(android), "--output", str(OUT),
            *(str(path) for path in classes.rglob("*.class")))

    for abi, target in (("arm64-v8a", "aarch64-linux-android26"),
                        ("armeabi-v7a", "armv7a-linux-androideabi26")):
        destination = OUT / abi / "libutilitytoast.so"
        destination.parent.mkdir(parents=True, exist_ok=True)
        run(str(compiler), f"--target={target}", "-shared", "-fPIC", "-O2",
            "-Wall", "-Wextra", "-Werror",
            "-fvisibility=hidden", "-Wl,-z,relro,-z,now", "-o", str(destination),
            str(ROOT / "android" / "native" / "toast_bridge.c"), "-ldl")
    print("Built Android toast DEX and JNI bridges in", OUT)


if __name__ == "__main__":
    main()
