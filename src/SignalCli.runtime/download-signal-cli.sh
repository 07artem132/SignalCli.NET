#!/usr/bin/env bash
set -euo pipefail

# post-modernize-tuning §8d.4-§8d.6 (audit N6): Version і Sha256 тепер можна
# передавати позиційними args (з csproj <SignalCliVersion>/<SignalCliSha256> через
# Exec Command). Дефолти лишаємо для прямого виклику скрипта без csproj-binding'у.
#
# Усі аргументи опціональні:
#   $1 — OUT_DIR  (default: signal-cli)
#   $2 — VERSION  (default: 0.14.6)
#   $3 — SHA256   (default: pinned value below)
OUT_DIR="${1:-signal-cli}"
VERSION="${2:-0.14.6}"
EXPECTED_SHA256="${3:-e90f4faea709b3c0a55909646a2b94289b9779ba9c8fd5c6eaa847d3f67312eb}"

if [ -f "$OUT_DIR/bin/signal-cli" ]; then
  echo "✅ signal-cli вже існує в $OUT_DIR, завантаження не потрібне."
  exit 0
fi

FILENAME="signal-cli-$VERSION.tar.gz"
URL="https://github.com/AsamK/signal-cli/releases/download/v$VERSION/$FILENAME"

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
