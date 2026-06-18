#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Close-flow smoke — POST /booking/{id}/close (Debt) CANLI DB'ye karşı, sonra TAM geri alır.
# Doğrular: BÖL kapsama (expired cüzdan → cover 0 → tüm süre ücret), borç defteri,
#           booking.Price, response özeti. booking 5 + wallet 1 + ledger tamamen restore edilir.
# ─────────────────────────────────────────────────────────────────────────────
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="http://localhost:5034"
export PGPASSWORD=1
PG() { psql -h localhost -p 5432 -U yusufeneszenler -d iroh_db -tA -c "$1"; }
sqlval() { [ -z "$1" ] && echo "NULL" || echo "'$1'"; }

SECRET=$(python3 -c "import json;print(json.load(open('$ROOT/appsettings.Development.json'))['JwtSettings']['SecretKey'])")
TOKEN=$(SECRET="$SECRET" node -e 'const c=require("crypto");const b=o=>Buffer.from(JSON.stringify(o)).toString("base64url");const n=Math.floor(Date.now()/1000);const h=b({alg:"HS256",typ:"JWT"});const p=b({sub:"1",id:"1",name:"smoke",email:"s@x.y",iss:"IrohAPI",aud:"IrohApp",iat:n,exp:n+600});console.log(h+"."+p+"."+c.createHmac("sha256",process.env.SECRET).update(h+"."+p).digest("base64url"));')

# Snapshot
COMPANY=$(PG "SELECT firsthourprice||','||additionalhalfhourprice FROM company LIMIT 1;")
FIRST=${COMPANY%,*}; HALF=${COMPANY#*,}
EXPECTED=$((FIRST + HALF))   # 90 dk = ilk saat + 1 yarım saat
SUBEND=$(PG "SELECT to_char(starttime + interval '90 minutes','YYYY-MM-DD\"T\"HH24:MI:SS') FROM bookings WHERE id=5;")
B5=$(PG "SELECT status||'~'||COALESCE(subscriptionstarttime::text,'')||'~'||COALESCE(subscriptionendtime::text,'')||'~'||COALESCE(endtime::text,'')||'~'||COALESCE(price::text,'') FROM bookings WHERE id=5;")
IFS='~' read -r ST SS SE ET PR <<< "$B5"
BT=$(PG "SELECT COALESCE(max(id),0) FROM time_ledger;")
BC=$(PG "SELECT COALESCE(max(id),0) FROM cash_ledger;")
echo "[snapshot] expected_charge=$EXPECTED subEnd=$SUBEND booking5_status=$ST"

( cd "$ROOT" && DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "$BASE" >/tmp/close-smoke.log 2>&1 ) &
restore() {
  lsof -ti:5034 2>/dev/null | xargs kill 2>/dev/null; sleep 1
  PG "UPDATE bookings SET status='$ST', subscriptionstarttime=$(sqlval "$SS"), subscriptionendtime=$(sqlval "$SE"), endtime=$(sqlval "$ET"), price=$(sqlval "$PR") WHERE id=5;" >/dev/null
  PG "DELETE FROM cash_ledger WHERE id > $BC; DELETE FROM time_ledger WHERE id > $BT;" >/dev/null
  PG "UPDATE wallets w SET time_balance_minutes=COALESCE((SELECT SUM(minutes_delta) FROM time_ledger t WHERE t.wallet_id=w.id),0), cash_balance=COALESCE((SELECT SUM(amount_delta) FROM cash_ledger c WHERE c.wallet_id=w.id),0);" >/dev/null
  echo "[restore] booking5 + wallet1 + ledger eski haline döndürüldü."
  echo "  booking5 status=$(PG "SELECT status FROM bookings WHERE id=5;") · wallet1=$(PG "SELECT time_balance_minutes||'/'||cash_balance FROM wallets WHERE customer_id=1;")"
}
trap restore EXIT

curl -s -o /dev/null --retry 90 --retry-connrefused --retry-delay 1 -H "Authorization: Bearer $TOKEN" "$BASE/api/table" || { echo "server kalkmadı"; tail -20 /tmp/close-smoke.log; exit 1; }

RESP=$(curl -s -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"settlement\":\"Debt\",\"subscriptionEndTime\":\"$SUBEND\",\"endTime\":\"$SUBEND\"}" \
  "$BASE/api/booking/5/close")
echo "[response] $RESP"
if [[ "$RESP" != *'"data"'* ]]; then
  echo "[server-log son satırlar]"; tail -25 /tmp/close-smoke.log
fi

EXPECTED="$EXPECTED" python3 -c "
import sys,json,os
d=json.load(sys.stdin)['data']
exp=float(os.environ['EXPECTED'])
checks={
  'durationMinutes==90': d['durationMinutes']==90,
  'uncoveredMinutes==90 (expired→cover0)': d['uncoveredMinutes']==90,
  'charged==expected': abs(float(d['charged'])-exp)<0.01,
  'walletAfter.debt==charged': abs(float(d['walletAfter']['debt'])-float(d['charged']))<0.01,
  'timeBalance unchanged 1758': d['walletAfter']['timeBalanceMinutes']==1758,
  'hasWallet': d['hasWallet']==True,
}
ok=all(checks.values())
for k,v in checks.items(): print(('  OK  ' if v else '  FAIL ')+k)
print('CLOSE-SMOKE:', 'OK' if ok else 'FAIL')
sys.exit(0 if ok else 1)
" <<< "$RESP"
