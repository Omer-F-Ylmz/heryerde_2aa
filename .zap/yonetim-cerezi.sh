#!/usr/bin/env bash
# Yönetici girişi yapıp ZAP replacer kuralının Cookie değerini yazar:
#   heryerde.admin=<oturum>;.AspNetCore.Antiforgery.<x>=<token>
# Tarama hesabında iki adımlı doğrulama kapalıdır (compose'un tohumladığı yönetici). Tohum yönetici ilk girişte parolasını
# değiştirmek zorunda olduğundan giriş /admin/sifre'ye yönlenirse parola bir kez değiştirilir (yeni damgalı çerez).
# Kullanım: yonetim-cerezi.sh <base> <e-posta> <parola>
set -euo pipefail
base="$1"
jar="$(mktemp)"

token_of() { grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' <<<"$1" | head -1 | sed 's/.*value="//;s/"$//'; }

html="$(curl -fsS -c "$jar" -b "$jar" "$base/admin/auth/login")"
result="$(curl -s -o /dev/null -w '%{http_code} %{redirect_url}' -c "$jar" -b "$jar" \
  --data-urlencode "__RequestVerificationToken=$(token_of "$html")" \
  --data-urlencode "Email=$2" --data-urlencode "Password=$3" "$base/admin/auth/login")"
if [ "${result%% *}" != "302" ]; then
  echo "Yönetici girişi başarısız (${result%% *})." >&2
  exit 1
fi

if [[ "${result#* }" == */admin/sifre ]]; then
  html="$(curl -fsS -c "$jar" -b "$jar" "$base/admin/sifre")"
  status="$(curl -s -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
    --data-urlencode "__RequestVerificationToken=$(token_of "$html")" \
    --data-urlencode "CurrentPassword=$3" --data-urlencode "NewPassword=$3-zap1" --data-urlencode "ConfirmPassword=$3-zap1" \
    "$base/admin/sifre")"
  if [ "$status" != "200" ]; then
    echo "İlk giriş parola değişimi başarısız ($status)." >&2
    exit 1
  fi
fi

awk -F'\t' 'NF == 7 && ($6 == "heryerde.admin" || $6 ~ /^\.AspNetCore\.Antiforgery\./) { printf "%s%s=%s", sep, $6, $7; sep = ";" }' "$jar"
