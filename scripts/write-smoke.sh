#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Write-path smoke (P6 güvence — parity-check.sh'in yazma tarafı tamamlayıcısı)
# Refactor sonrası PUT/DELETE uçlarının iş kurallarını doğrular:
#   idempotent PUT -> 200 · olmayan id -> 404 · sistem misafiri (999999) -> 400
# Veri DEĞERLERİNE dokunmaz (aynı değerlerle PUT). Gereksinim: parity-check.sh ile ayni.
#   ./scripts/write-smoke.sh   (çıkış 0 = tüm yazma kuralları tuttu)
# ─────────────────────────────────────────────────────────────────────────────
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="http://localhost:5034"
export PGPASSWORD=1

SECRET=$(python3 -c "import json;print(json.load(open('$ROOT/appsettings.Development.json'))['JwtSettings']['SecretKey'])")
TOKEN=$(node -e "const jwt=require('$ROOT/../server/node_modules/jsonwebtoken');console.log(jwt.sign({sub:'1',email:'smoke',name:'smoke'},'$SECRET',{expiresIn:'10m',issuer:'IrohAPI',audience:'IrohApp'}))")

echo "[write-smoke] .NET server başlatılıyor..."
( cd "$ROOT" && DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "$BASE" >/tmp/write-smoke-server.log 2>&1 ) &
trap 'lsof -ti:5034 2>/dev/null | xargs kill 2>/dev/null' EXIT

code(){ curl -s -o /dev/null -w "%{http_code}" -X "$1" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" ${3:+-d "$3"} "$BASE$2"; }
body(){ curl -s -H "Authorization: Bearer $TOKEN" "$BASE$1"; }

curl -s -o /dev/null --retry 40 --retry-connrefused --retry-delay 1 -H "Authorization: Bearer $TOKEN" "$BASE/api/table" || { echo "server kalkmadı (bkz /tmp/write-smoke-server.log)"; exit 1; }

PASS=0; FAIL=0
chk(){ if [ "$2" = "$3" ]; then printf "  \033[32m✓\033[0m %s (%s)\n" "$1" "$3"; PASS=$((PASS+1)); else printf "  \033[31m✗ %s — beklenen %s, gelen %s\033[0m\n" "$1" "$2" "$3"; FAIL=$((FAIL+1)); fi; }

echo "[write-smoke] yazma-yolu testleri:"

CO=$(body "/api/company")
CPL=$(echo "$CO" | python3 -c "import sys,json;d=json.load(sys.stdin)['data'][0];print(json.dumps({'id':d['id'],'name':d['name'],'firstHourPrice':d['firstHourPrice'],'additionalHalfHourPrice':d['additionalHalfHourPrice']}))")
chk "company PUT (idempotent)" 200 "$(code PUT /api/company "$CPL")"
chk "company PUT 404" 404 "$(code PUT /api/company '{"id":999998,"name":"x","firstHourPrice":1,"additionalHalfHourPrice":1}')"

TB=$(body "/api/table")
TPL=$(echo "$TB" | python3 -c "import sys,json;d=json.load(sys.stdin)['data'][0];print(json.dumps({'id':d['id'],'name':d['name']}))")
chk "table PUT (idempotent)" 200 "$(code PUT /api/table "$TPL")"
chk "table PUT 404" 404 "$(code PUT /api/table '{"id":999998,"name":"x"}')"

CU=$(body "/api/customer/4")
UPL=$(echo "$CU" | python3 -c "import sys,json;d=json.load(sys.stdin)['data'];print(json.dumps({'id':d['id'],'name':d['name'],'lastName':d['lastName'],'phone':d['phone'],'mail':d['mail']}))")
chk "customer PUT (idempotent)" 200 "$(code PUT /api/customer "$UPL")"
chk "customer PUT 404" 404 "$(code PUT /api/customer '{"id":999998,"name":"x","lastName":"y","phone":"0","mail":"a@b.c"}')"
chk "customer PUT 400 (misafir 999999)" 400 "$(code PUT /api/customer '{"id":999999,"name":"x","lastName":"y","phone":"0","mail":"a@b.c"}')"
chk "customer DELETE 404" 404 "$(code DELETE /api/customer/999998)"
chk "customer DELETE 400 (misafir 999999)" 400 "$(code DELETE /api/customer/999999)"

chk "bookinglog PUT 404" 404 "$(code PUT /api/booking-log/999998 '{"bookingId":1,"time":"2026-01-01T00:00:00Z","type":1,"userId":1}')"

echo
echo "[write-smoke] SONUÇ: $PASS geçti, $FAIL başarısız."
[ "$FAIL" -eq 0 ]
