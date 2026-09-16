#!/usr/bin/env bash
# CI (smoke, restore-check, zap-scan): prod imajı derlenmeden önce müşteriye görünen taslak metindeki yer tutucular örnek değerle
# doldurulur; imajın kendisi sınanır, metnin onayı değil. Gerçek yayında (deploy.yml) bu adım YOKTUR: yer tutucu kalmışsa
# Production'da /health/ready 503 döner ve smoke kırmızıdır (YAYIN-KAPI, HerYerde.Web/Infrastructure/PlaceholderAudit.cs).
set -euo pipefail

mapfile -t files < <(grep -rlE '\[(MÜŞTERİ|ÖNERİ)' HerYerde.Web/Views docs/sss.md || true)
if [ "${#files[@]}" -eq 0 ]; then
  echo "Taslak işareti yok."
  exit 0
fi

sed -i -E 's/\[MÜŞTERİ[^]]*\]/CI örnek değeri/g; s/\[ÖNERİ\]//g' "${files[@]}"
echo "Taslak işaretleri dolduruldu: ${#files[@]} dosya"
