#!/usr/bin/env bash
set -euo pipefail

OUT_DIR="${1:-signal-cli}"

if [ -f "$OUT_DIR/bin/signal-cli" ]; then
  echo "✅ signal-cli вже існує в $OUT_DIR, завантаження не потрібне."
  exit 0
fi

VERSION="0.14.3"
FILENAME="signal-cli-$VERSION.tar.gz"
URL="https://github.com/AsamK/signal-cli/releases/download/v$VERSION/$FILENAME"
# SHA-256 офіційного релізного архіву (звірено із завантаженням з GitHub Releases).
EXPECTED_SHA256="60a0a51312d07ed0cd6f4d5080b2ffe6ee838ea99f92297f2803e24af1826c6f"

TMP_DIR="$(mktemp -d)"
ARCHIVE="$TMP_DIR/$FILENAME"
# Прибираємо тимчасову директорію за будь-якого виходу
trap 'rm -rf "$TMP_DIR"' EXIT

echo "📥 Завантаження $FILENAME ..."
if command -v curl >/dev/null 2>&1; then
  curl -fSL -o "$ARCHIVE" "$URL"
elif command -v wget >/dev/null 2>&1; then
  wget -O "$ARCHIVE" "$URL"
else
  echo "❌ Не знайдено ні curl, ні wget. Встановіть один із них." >&2
  exit 2
fi

echo "🔐 Перевірка цілісності (SHA-256) ..."
if command -v sha256sum >/dev/null 2>&1; then
  ACTUAL_SHA256="$(sha256sum "$ARCHIVE" | awk '{print $1}')"
elif command -v shasum >/dev/null 2>&1; then
  ACTUAL_SHA256="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"
else
  echo "❌ Не знайдено ні sha256sum, ні shasum для перевірки цілісності." >&2
  exit 5
fi

if [ "$ACTUAL_SHA256" != "$EXPECTED_SHA256" ]; then
  echo "❌ Невідповідність SHA-256! Очікувано $EXPECTED_SHA256, отримано $ACTUAL_SHA256. Завантаження перервано." >&2
  exit 6
fi
echo "✅ SHA-256 збігається."

echo "📦 Розпаковка до $OUT_DIR ..."
mkdir -p "$OUT_DIR"
tar -xzf "$ARCHIVE" -C "$OUT_DIR" --strip-components=1

echo "✅ Готово! signal-cli v$VERSION встановлено у $OUT_DIR"
