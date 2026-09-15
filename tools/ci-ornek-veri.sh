#!/usr/bin/env bash
# CI (restore-check, smoke): compose ile açılmış boş prod veritabanına Ev > Mutfak kategorisi ve imajdaki --ithal ile tek ürün
# koyar, --gorsel-yenile'yi de imajda çalıştırır; ürün vitrinde görünsün diye etkinleştirilir. Seed prod'da kapalıdır.
set -euo pipefail

COMPOSE=${COMPOSE:-docker compose -f docker-compose.prod.yml}

sql() {
  $COMPOSE exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$HERYERDE_DB_PASSWORD" -C -b -d HerYerde -Q "$1"
}

sql "SET NOCOUNT ON;
INSERT INTO category (name, slug, parent_id, sort_order, is_active) VALUES (N'Ev', 'ev', NULL, 2, 1);
INSERT INTO category (name, slug, parent_id, sort_order, is_active) SELECT N'Mutfak', 'mutfak', id, 1, 1 FROM category WHERE slug = 'ev';"

# 64x64 düz renk PNG (ImageSharp'ın okuyacağı geçerli dosya); ek araç gerektirmez.
mkdir -p ci-raw
python3 - <<'EOF'
import struct, zlib
w = h = 64
raw = b''.join(b'\x00' + b'\xa8\x44\x2a' * w for _ in range(h))
def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b'')
open('ci-raw/ornek.png', 'wb').write(png)
EOF
printf '| dosya | ad | slug | kategori | aciklama |\n|---|---|---|---|---|\n| ornek.png | CI Ornek Tencere | ci-ornek-tencere | mutfak | CI ornek urunu |\n' > ci-raw/tablo.md

$COMPOSE run --rm -v "$PWD/ci-raw:/raw:ro" web --ithal /raw --tablo /raw/tablo.md
$COMPOSE run --rm web --gorsel-yenile

sql "SET NOCOUNT ON; UPDATE product SET is_active = 1, price = 349.90 WHERE id = (SELECT MAX(id) FROM product);"
