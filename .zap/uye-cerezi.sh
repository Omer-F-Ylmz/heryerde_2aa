#!/usr/bin/env bash
# Tarama üyesi açıp ZAP replacer kuralının Cookie değerine eklenecek üye oturumunu yazar: heryerde.customer=<oturum>
# Kayıt → doğrulama bağlantısı outbox'tan okunur (CI'da SMTP gerçek değil) → parolayla doğrulanır; doğrulama oturumu açar.
# Kullanım: uye-cerezi.sh <base> <e-posta> <parola>   (COMPOSE ve HERYERDE_DB_PASSWORD ortamdan)
set -euo pipefail
base="$1"
COMPOSE=${COMPOSE:-docker compose -f docker-compose.prod.yml}
jar="$(mktemp)"

form_token() { grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' <<<"$1" | head -1 | sed 's/.*value="//;s/"$//'; }

html="$(curl -fsS -c "$jar" -b "$jar" "$base/hesap/kayit")"
status="$(curl -s -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
  --data-urlencode "__RequestVerificationToken=$(form_token "$html")" \
  --data-urlencode "Email=$2" --data-urlencode "Password=$3" --data-urlencode "KvkkConsent=true" "$base/hesap/kayit")"
if [ "$status" != "200" ]; then
  echo "Üye kaydı başarısız ($status)." >&2
  exit 1
fi

# Bağlantı: hesap/dogrula?t=<43 karakter base64url>
token="$($COMPOSE exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$HERYERDE_DB_PASSWORD" -C -b -h -1 -W -d HerYerde -Q "SET NOCOUNT ON;
SELECT TOP 1 SUBSTRING(body, CHARINDEX('hesap/dogrula?t=', body) + 16, 43) FROM outbox_message
WHERE [to] = N'$2' AND CHARINDEX('hesap/dogrula?t=', body) > 0 ORDER BY id DESC" | tr -d '\r' | grep -E '^[A-Za-z0-9_-]{43}$' | head -1)"
if [ -z "$token" ]; then
  echo "Doğrulama bağlantısı outbox'ta yok." >&2
  exit 1
fi

html="$(curl -fsS -c "$jar" -b "$jar" "$base/hesap/dogrula?t=$token")"
status="$(curl -s -o /dev/null -w '%{http_code}' -c "$jar" -b "$jar" \
  --data-urlencode "__RequestVerificationToken=$(form_token "$html")" \
  --data-urlencode "Token=$token" --data-urlencode "Password=$3" "$base/hesap/dogrula")"
if [ "$status" != "302" ]; then
  echo "Üye doğrulaması başarısız ($status)." >&2
  exit 1
fi

awk -F'\t' 'NF == 7 && $6 == "heryerde.customer" { printf "%s=%s", $6, $7 }' "$jar"
