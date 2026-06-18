#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Wallet smoke — cüzdan API'sini CANLI DB'ye karşı doğrular, sonra GERİ TEMİZLER.
# Doğrular: EF↔Postgres eşlemesi (3 yeni tablo), enum→string INSERT, statü türetimi,
#           credit / settle uçları, JSON serileştirme.
# Eklediği tüm ledger satırlarını siler ve bakiyeleri yeniden hesaplar (idempotent).
#   ./scripts/wallet-smoke.sh   (çıkış 0 = tüm kontroller geçti)
# ─────────────────────────────────────────────────────────────────────────────
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="http://localhost:5034"
export PGPASSWORD=1
PSQL=(psql -h localhost -p 5432 -U yusufeneszenler -d iroh_db -tA)

SECRET=$(python3 -c "import json;print(json.load(open('$ROOT/appsettings.Development.json'))['JwtSettings']['SecretKey'])")
TOKEN=$(SECRET="$SECRET" node -e '
const c=require("crypto");
const b64=o=>Buffer.from(typeof o==="string"?o:JSON.stringify(o)).toString("base64url");
const now=Math.floor(Date.now()/1000);
const h=b64({alg:"HS256",typ:"JWT"});
const p=b64({sub:"1",id:"1",name:"smoke",email:"smoke@x.y",iss:"IrohAPI",aud:"IrohApp",iat:now,exp:now+600});
const s=c.createHmac("sha256",process.env.SECRET).update(h+"."+p).digest("base64url");
console.log(h+"."+p+"."+s);')

# Temizlik için baseline: bu id'lerden sonraki tüm ledger satırları smoke'a aittir.
BT=$("${PSQL[@]}" -c "SELECT COALESCE(max(id),0) FROM time_ledger;")
BC=$("${PSQL[@]}" -c "SELECT COALESCE(max(id),0) FROM cash_ledger;")

echo "[wallet-smoke] .NET server başlatılıyor..."
( cd "$ROOT" && DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "$BASE" >/tmp/wallet-smoke-server.log 2>&1 ) &
cleanup(){
  lsof -ti:5034 2>/dev/null | xargs kill 2>/dev/null; sleep 1
  "${PSQL[@]}" -c "DELETE FROM cash_ledger WHERE id > $BC; DELETE FROM time_ledger WHERE id > $BT;
    UPDATE wallets w SET
      time_balance_minutes = COALESCE((SELECT SUM(minutes_delta) FROM time_ledger t WHERE t.wallet_id=w.id),0),
      cash_balance         = COALESCE((SELECT SUM(amount_delta) FROM cash_ledger c WHERE c.wallet_id=w.id),0);" >/dev/null
  echo "[cleanup] smoke satırları silindi, bakiyeler yeniden hesaplandı."
}
trap cleanup EXIT

curl -s -o /dev/null --retry 90 --retry-connrefused --retry-delay 1 -H "Authorization: Bearer $TOKEN" "$BASE/api/table" \
  || { echo "server kalkmadı (bkz /tmp/wallet-smoke-server.log)"; tail -20 /tmp/wallet-smoke-server.log; exit 1; }

body(){ curl -s -H "Authorization: Bearer $TOKEN" "$BASE$1"; }
post(){ curl -s -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "$2" "$BASE$1"; }
gint(){ python3 -c "import sys,json;print(json.load(sys.stdin)['data']['$1'])"; }
gflt(){ python3 -c "import sys,json;print(float(json.load(sys.stdin)['data']['$1']))"; }

PASS=0; FAIL=0
chk(){ if [ "$2" = "$3" ]; then printf "  \033[32m✓\033[0m %s (%s)\n" "$1" "$3"; PASS=$((PASS+1)); else printf "  \033[31m✗ %s — beklenen %s gelen %s\033[0m\n" "$1" "$2" "$3"; FAIL=$((FAIL+1)); fi; }

echo "[wallet-smoke] kontroller:"
# 1) Backfill'i salt-okunur doğrula (EF→Postgres, enum read, statü, serileştirme)
W1=$(body "/api/wallet/1")
chk "GET wallet/1 balance"            1758         "$(echo "$W1" | gint timeBalanceMinutes)"
chk "GET wallet/1 status (expired)"   "Subscriber" "$(echo "$W1" | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['status'])")"

# 2) credit (+30dk, money 100 → Charge+Payment net 0) — enum string INSERT + FK
W2=$(post "/api/wallet/credit" '{"customerId":2,"minutes":30,"money":100}')
chk "POST credit time balance"        1230 "$(echo "$W2" | gint timeBalanceMinutes)"
chk "POST credit cash net 0"          "0.0" "$(echo "$W2" | gflt cashBalance)"

# 3) settle (+50) — cash Payment INSERT
W3=$(post "/api/wallet/settle" '{"customerId":2,"amount":50}')
chk "POST settle cash balance"        "50.0" "$(echo "$W3" | gflt cashBalance)"

echo
echo "[wallet-smoke] SONUÇ: $PASS geçti, $FAIL başarısız."
[ "$FAIL" -eq 0 ]
