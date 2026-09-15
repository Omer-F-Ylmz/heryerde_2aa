#!/usr/bin/env bash
# Yönetici girişi yapıp ZAP replacer kuralının Cookie değerini yazar:
#   heryerde.admin=<oturum>;.AspNetCore.Antiforgery.<x>=<token>
# Tarama hesabında iki adımlı doğrulama kapalıdır (compose'un tohumladığı yönetici). Kullanım: yonetim-cerezi.sh <base> <e-posta> <parola>
set -euo pipefail
base="$1"
jar="$(mktemp)"

html="$(curl -fsS -c "$jar" -b "$jar" "$base/admin/auth/login")"
token="$(grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' <<<"$html" | head -1 | sed 's/.*value="//;s/"$//')"
status="$(curl -s -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
  --data-urlencode "__RequestVerificationToken=$token" \
  --data-urlencode "Email=$2" --data-urlencode "Password=$3" "$base/admin/auth/login")"
if [ "$status" != "302" ]; then
  echo "Yönetici girişi başarısız ($status)." >&2
  exit 1
fi

awk -F'\t' 'NF == 7 && ($6 == "heryerde.admin" || $6 ~ /^\.AspNetCore\.Antiforgery\./) { printf "%s%s=%s", sep, $6, $7; sep = ";" }' "$jar"
