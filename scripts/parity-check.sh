#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Parite-diff harness (P6 regresyon güvenlik ağı)
# .NET API uçlarını canlı iroh_db proc/view'larıyla otomatik karşılaştırır.
# Cutover ÖNCESİ ve her P5 refactor SONRASI çalıştır: "API çıktısı proc ile aynı mı?"
#
# Kullanım:  ./scripts/parity-check.sh
# Gereksinim: psql (iroh_db), node (../server/node_modules/jsonwebtoken), python3, dotnet
# Çıkış kodu: 0 = tüm parite tuttu, 1 = en az bir sapma.
# ─────────────────────────────────────────────────────────────────────────────
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="http://localhost:5034"
NORM="$ROOT/scripts/_parity_normalize.py"
export PGPASSWORD=1
export PGOPTIONS="-c timezone=Europe/Istanbul"   # proc'lar TR session TZ'de çalışsın (SET echo'su olmadan)
S="2026-04-01"; E="2026-07-01"                    # dashboard tarih aralığı

SECRET=$(python3 -c "import json;print(json.load(open('$ROOT/appsettings.Development.json'))['JwtSettings']['SecretKey'])")
TOKEN=$(node -e "const jwt=require('$ROOT/../server/node_modules/jsonwebtoken');console.log(jwt.sign({sub:'1',email:'parity',name:'parity'},'$SECRET',{expiresIn:'10m',issuer:'IrohAPI',audience:'IrohApp'}))")

echo "[parity] .NET server başlatılıyor..."
( cd "$ROOT" && DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "$BASE" >/tmp/parity-server.log 2>&1 ) &
trap 'lsof -ti:5034 2>/dev/null | xargs kill 2>/dev/null' EXIT

api(){ curl -s -H "Authorization: Bearer $TOKEN" "$BASE$1"; }
proc(){ psql -U yusufeneszenler -h localhost -d iroh_db -tAc "$1"; }
norm(){ python3 "$NORM" "$1"; }

curl -s -o /dev/null --retry 40 --retry-connrefused --retry-delay 1 -H "Authorization: Bearer $TOKEN" "$BASE/api/table" || { echo "server ayağa kalkmadı (bkz /tmp/parity-server.log)"; exit 1; }

PASS=0; FAIL=0
check(){ # name expected actual
  if [ "$2" = "$3" ]; then printf "  \033[32m✓\033[0m %s\n" "$1"; PASS=$((PASS+1))
  else printf "  \033[31m✗ %s\033[0m\n     proc: %s\n     .net: %s\n" "$1" "$2" "$3"; FAIL=$((FAIL+1)); fi
}

echo "[parity] karşılaştırmalar:"

check "customers (fn_get_customers)" \
  "$(proc "SELECT string_agg(id||':'||status,',' ORDER BY id) FROM fn_get_customers(NULL,-1,50,NULL,NULL);")" \
  "$(api "/api/customer?page=-1" | norm customers)"

check "bookings (usp_get_bookings)" \
  "$(proc "SELECT string_agg(id::text,',' ORDER BY id DESC) FROM usp_get_bookings(-1,20,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL);")" \
  "$(api "/api/booking?page=-1" | norm bookings)"

check "active-bookings (vw_activebookings)" \
  "$(proc "SELECT string_agg(id||'|'||COALESCE(customer->>'status','-')||'|'||COALESCE(customer->'purchase'->>'id','-')||'|'||COALESCE(round((customer->'purchase'->>'usedHours')::numeric)::text,'-'),',' ORDER BY id) FROM vw_activebookings;")" \
  "$(api "/api/booking/Active" | norm active)"

check "search-unified (fn_search_unified)" \
  "$(proc "SELECT string_agg(COALESCE(child_id,0)||'|'||parent_id||'|'||status||'|'||round(COALESCE(remaining_hours,0),2)::text||'|'||is_active,',' ORDER BY parent_id,COALESCE(child_id,0)) FROM fn_search_unified('');")" \
  "$(api "/api/child/search-unified?q=" | norm search)"

check "tables (vw_tables)" \
  "$(proc "SELECT string_agg(id||':'||name,',' ORDER BY id) FROM vw_tables;")" \
  "$(api "/api/table" | norm tables)"

check "purchases (vw_purchases)" \
  "$(proc "SELECT string_agg(id||'|'||round(hours::numeric,2)||'|'||round(price::numeric,2)||'|'||customer_id,',' ORDER BY id) FROM vw_purchases;")" \
  "$(api "/api/purchase" | norm purchases)"

check "purchase/customer=4 (fn_get_purchase_by_customer_id)" \
  "$(proc "SELECT string_agg(id||'|'||round(\"usedHours\"::numeric)||'|'||json_array_length(payments),',' ORDER BY id) FROM fn_get_purchase_by_customer_id(4);")" \
  "$(api "/api/purchase/customer?customerId=4" | norm purchasecustomer)"

api "/api/dashboard/summary?startDate=$S&endDate=$E" > /tmp/parity-dash.json
check "dashboard.overview (fn_get_dashboard_overview/_purchases)" \
  "$(proc "SELECT o.total_children||'|'||o.active_currently||'|'||round(o.booking_revenue)||'|'||round(o.avg_duration_minutes)||'|'||o.subscription_sessions||'|'||pu.purchase_count||'|'||round(pu.purchase_revenue) FROM fn_get_dashboard_overview('$S','$E') o, fn_get_dashboard_purchases('$S','$E') pu;")" \
  "$(norm dash-overview < /tmp/parity-dash.json)"
check "dashboard.busyHours (fn_get_dashboard_busy_hours)" \
  "$(proc "SELECT string_agg(hour||'='||count,',' ORDER BY hour) FROM fn_get_dashboard_busy_hours('$S','$E');")" \
  "$(norm dash-busy < /tmp/parity-dash.json)"
check "dashboard.topCustomers (fn_get_dashboard_top_customers)" \
  "$(proc "SELECT string_agg(customer_id||'|'||round(total_spent),',' ORDER BY customer_id) FROM fn_get_dashboard_top_customers('$S','$E');")" \
  "$(norm dash-top < /tmp/parity-dash.json)"

echo
echo "[parity] SONUÇ: $PASS geçti, $FAIL sapma."
[ "$FAIL" -eq 0 ]
