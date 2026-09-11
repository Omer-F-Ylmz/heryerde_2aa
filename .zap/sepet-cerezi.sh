#!/usr/bin/env bash
# Sitemap'teki ilk eklenebilir ürünü sepete koyar ve ZAP replacer kuralının Cookie değerini yazar:
#   heryerde.cart=<guid>;.AspNetCore.Antiforgery.<x>=<token>
# Antiforgery çerezi de taşınır ki ZAP'ın gönderdiği formlar sunucunun ürettiği token'la eşleşsin.
set -euo pipefail
base="${1:-http://localhost:8080}"
jar="$(mktemp)"

for path in $(curl -fsS "$base/sitemap.xml" | grep -o '/urun/[^<]*'); do
  html="$(curl -fsS -c "$jar" -b "$jar" "$base$path")"
  token="$(grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' <<<"$html" | head -1 | sed 's/.*value="//;s/"$//')" || continue
  product="$(grep -o 'name="productId" value="[0-9]*"' <<<"$html" | head -1 | grep -o '[0-9]*"$' | tr -d '"')" || continue
  status="$(curl -s -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
    --data-urlencode "__RequestVerificationToken=$token" \
    --data-urlencode "productId=$product" -d "variantId=&quantity=1&donus=/sepet" "$base/sepet/ekle")"
  if [ "$status" = "302" ]; then
    awk -F'\t' 'NF == 7 && ($6 == "heryerde.cart" || $6 ~ /^\.AspNetCore\.Antiforgery\./) { printf "%s%s=%s", sep, $6, $7; sep = ";" }' "$jar"
    exit 0
  fi
done

echo "Sepete eklenebilen ürün bulunamadı." >&2
exit 1
