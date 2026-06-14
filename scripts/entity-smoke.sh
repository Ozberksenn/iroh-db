#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Entity-mapping smoke (P6 güvence — parity-check.sh'in kapsamadığı entity'ler)
# Bozuk bir [Column("...")] eşlemesi -> EF yanlış SQL kolonu üretir -> 500.
# Bu uçlar harness'ta yok; 200 (read) / 401 (bogus login) bekleriz (500 DEĞİL).
#   ./scripts/entity-smoke.sh   (çıkış 0 = tüm entity eşlemeleri sağlam)
# ─────────────────────────────────────────────────────────────────────────────
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="http://localhost:5034"
export PGPASSWORD=1

SECRET=$(python3 -c "import json;print(json.load(open('$ROOT/appsettings.Development.json'))['JwtSettings']['SecretKey'])")
TOKEN=$(node -e "const jwt=require('$ROOT/../server/node_modules/jsonwebtoken');console.log(jwt.sign({sub:'1',email:'smoke',name:'smoke'},'$SECRET',{expiresIn:'10m',issuer:'IrohAPI',audience:'IrohApp'}))")

echo "[entity-smoke] .NET server başlatılıyor..."
( cd "$ROOT" && DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "$BASE" >/tmp/entity-smoke-server.log 2>&1 ) &
trap 'lsof -ti:5034 2>/dev/null | xargs kill 2>/dev/null' EXIT

gcode(){ curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" "$BASE$1"; }
pcode(){ curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d "$2" "$BASE$1"; }
curl -s -o /dev/null --retry 40 --retry-connrefused --retry-delay 1 -H "Authorization: Bearer $TOKEN" "$BASE/api/table" || { echo "server kalkmadı (bkz /tmp/entity-smoke-server.log)"; exit 1; }

PASS=0; FAIL=0
chk(){ if [ "$2" = "$3" ]; then printf "  \033[32m✓\033[0m %s (%s)\n" "$1" "$3"; PASS=$((PASS+1)); else printf "  \033[31m✗ %s — beklenen %s, gelen %s\033[0m\n" "$1" "$2" "$3"; FAIL=$((FAIL+1)); fi; }

echo "[entity-smoke] harness-dışı entity mapping testleri:"
chk "GET /api/package (Package)"                          200 "$(gcode /api/package)"
chk "GET /api/company (Company)"                          200 "$(gcode /api/company)"
chk "GET /api/booking-log (BookingLog+Booking nav)"       200 "$(gcode /api/booking-log)"
chk "GET /api/purchase-payment (PurchasePayment)"         200 "$(gcode /api/purchase-payment)"
chk "GET purchase-bookings-by-id (PurchaseBooking join)"  200 "$(gcode '/api/purchase/purchase-bookings-by-id?purchaseId=1')"
chk "POST /api/auth/login bogus (User SELECT -> 401)"     401 "$(pcode /api/auth/login '{"mail":"nope@nope.invalid","password":"x"}')"

echo
echo "[entity-smoke] SONUÇ: $PASS geçti, $FAIL başarısız."
[ "$FAIL" -eq 0 ]
