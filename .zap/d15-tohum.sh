#!/usr/bin/env bash
# ZAP taraması (D15): tools/ci-ornek-veri.sh'nin koyduğu tek ürüne marka ve özellik, bilinen adres ve yönetim anahtarıyla bir
# çeyiz listesi ekler; .zap/yonetim.yaml'daki d15 bağlamı öneri ucunu, süzgeç panelini, çeyiz sayfalarını ve video formunu tarar.
set -euo pipefail

COMPOSE=${COMPOSE:-docker compose -f docker-compose.prod.yml}

$COMPOSE exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$HERYERDE_DB_PASSWORD" -C -b -d HerYerde -Q "SET NOCOUNT ON;
INSERT INTO brand (name, slug, logo_url) VALUES (N'Karaca', 'karaca', NULL);
UPDATE product SET brand_id = (SELECT id FROM brand WHERE slug = 'karaca') WHERE id = (SELECT MAX(id) FROM product);
INSERT INTO product_attribute (product_id, name, value, sort_order) SELECT MAX(id), N'Malzeme', N'Döküm', 0 FROM product;
INSERT INTO gift_registry (slug, manage_token, owner_name, phone, email, event_date, message, is_public, created_at)
VALUES ('zaptarama1', '0d15a000-0000-4000-8000-00000000d15a', N'Zap Tarama', '05000000000', NULL, DATEADD(day, 60, CAST(SYSUTCDATETIME() AS date)), N'Tarama listesi', 1, SYSUTCDATETIME());
INSERT INTO gift_registry_item (gift_registry_id, product_id, variant_id, desired_qty, received_qty)
SELECT r.id, (SELECT MAX(id) FROM product), NULL, 3, 0 FROM gift_registry r WHERE r.slug = 'zaptarama1';"
