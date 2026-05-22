#!/usr/bin/env bash
set -euo pipefail

# Downloads, verifies (SHA-256) and normalizes an Eclipse Temurin JRE so that
# "<OUT_DIR>/bin/java[.exe]" exists. Bundled by the SignalCli.Runtime.Jre.*
# packages so consumers do not need a system Java installation.
#
# Usage: download-jre.sh <OUT_DIR> <FEATURE> <VERSION> <OS> <ARCH> <SHA256>
#   FEATURE  e.g. 25
#   VERSION  e.g. 25.0.3+9
#   OS       windows | mac
#   ARCH     x64 | aarch64

OUT_DIR="$1"
FEATURE="$2"
VERSION="$3"
OS="$4"
ARCH="$5"
EXPECTED_SHA256="$6"

# Idempotent: skip if a JRE is already present (offline / incremental builds).
if [ -f "$OUT_DIR/bin/java" ] || [ -f "$OUT_DIR/bin/java.exe" ]; then
  echo "JRE already present in $OUT_DIR, skipping download."
  exit 0
fi

TAG_VER="${VERSION/+/%2B}"   # release tag: jdk-25.0.3%2B9
FN_VER="${VERSION/+/_}"      # asset version: 25.0.3_9
if [ "$OS" = "windows" ]; then EXT="zip"; else EXT="tar.gz"; fi
URL="https://github.com/adoptium/temurin$FEATURE-binaries/releases/download/jdk-$TAG_VER/OpenJDK${FEATURE}U-jre_${ARCH}_${OS}_hotspot_$FN_VER.$EXT"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
ARCHIVE="$TMP_DIR/jre.$EXT"

echo "Downloading $URL ..."
if command -v curl >/dev/null 2>&1; then
  curl -fSL -o "$ARCHIVE" "$URL"
elif command -v wget >/dev/null 2>&1; then
  wget -O "$ARCHIVE" "$URL"
else
  echo "ERROR: neither curl nor wget found." >&2
  exit 2
fi

echo "Verifying SHA-256 ..."
if command -v sha256sum >/dev/null 2>&1; then
  ACTUAL_SHA256="$(sha256sum "$ARCHIVE" | awk '{print $1}')"
elif command -v shasum >/dev/null 2>&1; then
  ACTUAL_SHA256="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"
else
  echo "ERROR: neither sha256sum nor shasum found." >&2
  exit 5
fi
if [ "$ACTUAL_SHA256" != "$EXPECTED_SHA256" ]; then
  echo "ERROR: SHA-256 mismatch! expected $EXPECTED_SHA256 but got $ACTUAL_SHA256." >&2
  exit 6
fi
echo "SHA-256 OK."

EXTRACT="$TMP_DIR/extract"
mkdir -p "$EXTRACT"
case "$EXT" in
  zip)
    if command -v unzip >/dev/null 2>&1; then
      unzip -q "$ARCHIVE" -d "$EXTRACT"
    else
      tar -xf "$ARCHIVE" -C "$EXTRACT"   # bsdtar/libarchive can read zip
    fi
    ;;
  tar.gz)
    tar -xzf "$ARCHIVE" -C "$EXTRACT"
    ;;
  *)
    echo "ERROR: unknown archive type: $EXT" >&2
    exit 7
    ;;
esac

# Locate the JRE home: the directory whose 'bin' subfolder contains java[.exe].
# Handles Windows (<root>/bin/java.exe) and macOS (<root>/Contents/Home/bin/java).
JAVA_BIN="$(find "$EXTRACT" -type f \( -name java -o -name java.exe \) -path '*/bin/*' | head -n1)"
if [ -z "$JAVA_BIN" ]; then
  echo "ERROR: java executable not found after extraction." >&2
  exit 8
fi
JRE_HOME="$(dirname "$(dirname "$JAVA_BIN")")"

echo "Normalizing JRE layout into $OUT_DIR ..."
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"
cp -a "$JRE_HOME/." "$OUT_DIR/"
chmod +x "$OUT_DIR/bin/java" 2>/dev/null || true

echo "JRE (Temurin $VERSION, $OS/$ARCH) installed to $OUT_DIR"
