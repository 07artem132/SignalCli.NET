#!/usr/bin/env bash
set -euo pipefail

# Завантажує НАТИВНИЙ (GraalVM) бінарник signal-cli — без JVM.
# Офіційні native-білди доступні лише для Linux x64.
OUT_DIR="${1:-signal-cli-native}"

if [ -f "$OUT_DIR/signal-cli" ]; then
  echo "✅ native signal-cli вже існує в $OUT_DIR, завантаження не потрібне."
  exit 0
fi

VERSION="0.14.6"
FILENAME="signal-cli-$VERSION-Linux-native.tar.gz"
URL="https://github.com/AsamK/signal-cli/releases/download/v$VERSION/$FILENAME"
# SHA-256 офіційного нативного релізного архіву (звірено із завантаженням з GitHub Releases).
EXPECTED_SHA256="c78639c2d3c14cd004872a99ecf129bd7d7c26ee7d9844d50c2b0afdafefea68"

TMP_DIR="$(mktemp -d)"
ARCHIVE="$TMP_DIR/$FILENAME"
trap 'rm -rf "$TMP_DIR"' EXIT

echo "📥 Завантаження $FILENAME ..."
if command -v curl >/dev/null 2>&1; then
  curl -fSL -o "$ARCHIVE" "$URL"
elif command -v wget >/dev/null 2>&1; then
  wget -O "$ARCHIVE" "$URL"
else
  echo "❌ Не знайдено ні curl, ні wget." >&2
  exit 2
fi

echo "🔐 Перевірка цілісності (SHA-256) ..."
if command -v sha256sum >/dev/null 2>&1; then
  ACTUAL_SHA256="$(sha256sum "$ARCHIVE" | awk '{print $1}')"
elif command -v shasum >/dev/null 2>&1; then
  ACTUAL_SHA256="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"
else
  echo "❌ Не знайдено ні sha256sum, ні shasum." >&2
  exit 5
fi

if [ "$ACTUAL_SHA256" != "$EXPECTED_SHA256" ]; then
  echo "❌ Невідповідність SHA-256! Очікувано $EXPECTED_SHA256, отримано $ACTUAL_SHA256." >&2
  exit 6
fi
echo "✅ SHA-256 збігається."

echo "📦 Розпаковка до $OUT_DIR ..."
mkdir -p "$OUT_DIR"
tar -xzf "$ARCHIVE" -C "$OUT_DIR"
chmod +x "$OUT_DIR/signal-cli"

echo "✅ Готово! native signal-cli v$VERSION встановлено у $OUT_DIR (Java не потрібна)"
