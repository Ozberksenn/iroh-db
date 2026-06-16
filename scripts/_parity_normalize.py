#!/usr/bin/env python3
# parity-check.sh yardımcısı: .NET ApiResponse JSON'unu stdin'den okuyup
# verilen uç için karşılaştırılabilir bir imza string'i basar.
import sys, json

kind = sys.argv[1]
doc = json.load(sys.stdin)
data = doc.get("data") if isinstance(doc, dict) else doc


def join(rows):
    print(",".join(rows))


if kind == "customers":
    items = sorted(data["items"], key=lambda x: x["id"])
    join(f"{r['id']}:{r['status']}" for r in items)

elif kind == "bookings":
    join(str(r["id"]) for r in data["items"])

elif kind == "active":
    def row(b):
        c = b.get("customer") or {}
        p = c.get("purchase") or {}
        pid = str(p["id"]) if p else "-"
        um = str(round(float(p["usedMinutes"]))) if p else "-"
        return f"{b['id']}|{c.get('status', '-')}|{pid}|{um}"
    join(row(b) for b in sorted(data, key=lambda x: x["id"]))

elif kind == "search":
    def row(r):
        return f"{r['child_id']}|{r['parent_id']}|{r['status']}|{float(r['remaining_hours']):.2f}|{str(r['is_active']).lower()}"
    join(row(r) for r in sorted(data, key=lambda x: (x["parent_id"], x["child_id"])))

elif kind == "tables":
    join(f"{t['id']}:{t['name']}" for t in sorted(data, key=lambda x: x["id"]))

elif kind == "purchases":
    join(f"{p['id']}|{p['hours']:.2f}|{p['price']:.2f}|{p['customerId']}" for p in sorted(data, key=lambda x: x["id"]))

elif kind == "purchasecustomer":
    join(f"{p['id']}|{round(float(p['usedMinutes']))}|{len(p['payments'])}" for p in sorted(data, key=lambda x: x["id"]))

elif kind == "dash-overview":
    o = data["overview"]
    print(f"{o['totalBookings']}|{o['activeCurrently']}|{round(o['bookingRevenue'])}|{o['averageDurationMinutes']}|{o['subscriptionSessions']}|{o['purchaseCount']}|{round(o['purchaseRevenue'])}")

elif kind == "dash-busy":
    join(f"{r['hour']}={r['count']}" for r in data["busyHoursChart"])

elif kind == "dash-top":
    join(f"{c['id']}|{round(c['totalSpent'])}" for c in sorted(data["topCustomers"], key=lambda x: x["id"]))

else:
    sys.exit(f"bilinmeyen kind: {kind}")
