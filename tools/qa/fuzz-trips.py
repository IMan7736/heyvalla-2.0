#!/usr/bin/env python3
"""LOOKOUT: adversarial search fuzz against a running API. Usage: fuzz-trips.py http://localhost:5110/v1 [N] [seed]
Invariants: every connection gap >= required buffer and equals the reported buffer; depart-after never
earlier than asked; arrive-by never later than asked; duration/price arithmetic; empty results carry notices."""
import json, random, sys, urllib.request, urllib.error
from datetime import datetime, timedelta, timezone
A = sys.argv[1]; N = int(sys.argv[2]) if len(sys.argv) > 2 else 300; random.seed(int(sys.argv[3]) if len(sys.argv) > 3 else 7)
MV = timezone(timedelta(hours=5))
def call(path, body=None):
    req = urllib.request.Request(A + path, data=json.dumps(body).encode() if body else None, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as r: return r.status, json.load(r)
    except urllib.error.HTTPError as e: return e.code, json.loads(e.read() or b"{}")
places = []; cur = None
while True:
    s, d = call("/places?limit=200" + (f"&cursor={cur}" if cur else "")); places += d["items"]; cur = d["page"].get("nextCursor")
    if not cur: break
usable = [p for p in places if p["type"] in ("island", "airport", "jetty", "taxi_stand")]
P = lambda t: datetime.fromisoformat(t.replace("Z", "+00:00"))
bad = []; stats = dict(searches=0, with_options=0, empty=0, empty_no_notice=0, options=0, errors={}, nlegs=0)
for i in range(N):
    a, b = random.sample(usable, 2)
    day = datetime.now(MV).date() + timedelta(days=random.choice([0, 1, 2, 3, 7, 30]))
    hh, mm = random.choice([(0, 0), (4, 30), (5, 0), (8, 0), (12, 15), (17, 45), (21, 0), (23, 30), (23, 59), random.choice([(h, m) for h in range(24) for m in (0, 15, 30, 45)])])
    mode = random.choice(["departAfter", "arriveBy"]); risk = random.choice(["cautious", "balanced", "relaxed"])
    pax = {"adults": random.randint(1, 4), "children": random.randint(0, 2)}
    body = {"from": a["id"], "to": b["id"], "date": day.isoformat(), "time": f"{hh:02d}:{mm:02d}", "timeMode": mode, "passengers": pax, "preferences": {"riskTolerance": risk}}
    st, r = call("/trips/search", body); stats["searches"] += 1
    tag = f'{a["name"]["en"]}->{b["name"]["en"]} {day} {hh:02d}:{mm:02d} {mode} {risk}'
    if st != 200: stats["errors"][st] = stats["errors"].get(st, 0) + 1; bad.append(("HTTP", st, tag, r.get("code"))); continue
    req_t = datetime(day.year, day.month, day.day, hh, mm, tzinfo=MV)
    if not r["options"]:
        stats["empty"] += 1
        if not r.get("notices"): stats["empty_no_notice"] += 1; bad.append(("EMPTY_NO_NOTICE", tag))
        continue
    stats["with_options"] += 1; ids = [o["id"] for o in r["options"]]
    if len(ids) != len(set(ids)): bad.append(("DUP_IDS", tag))
    for o in r["options"]:
        stats["options"] += 1; legs = o["legs"]; stats["nlegs"] += len(legs)
        if mode == "departAfter" and P(legs[0]["departAt"]) < req_t: bad.append(("DEPART_BEFORE_REQUEST", tag, legs[0]["departAt"]))
        if mode == "arriveBy" and P(legs[-1]["arriveAt"]) > req_t: bad.append(("ARRIVE_AFTER_DEADLINE", tag, legs[-1]["arriveAt"]))
        if P(o["departAt"]) != P(legs[0]["departAt"]) or P(o["arriveAt"]) != P(legs[-1]["arriveAt"]): bad.append(("OPTION_TIMES_MISMATCH", tag))
        if int((P(o["arriveAt"]) - P(o["departAt"])).total_seconds() // 60) != o["durationMin"]: bad.append(("DURATION_MISMATCH", tag, o["durationMin"]))
        total = sum(l["price"]["amountMinor"] for l in legs)
        if total != o["price"]["total"]["amountMinor"]: bad.append(("PRICE_SUM_MISMATCH", tag, total, o["price"]["total"]["amountMinor"]))
        bufs = {x["afterLegIndex"]: x for x in o["buffers"]}
        for k in range(len(legs) - 1):
            gap = int((P(legs[k + 1]["departAt"]) - P(legs[k]["arriveAt"])).total_seconds() // 60)
            if gap < 0: bad.append(("OVERLAP", tag, k, gap))
            x = bufs.get(k)
            if x:
                if gap < x["requiredMinutes"]: bad.append(("BUFFER_VIOLATED", tag, k, gap, x["requiredMinutes"]))
                if gap != x["minutes"]: bad.append(("BUFFER_REPORT_WRONG", tag, k, gap, x["minutes"]))
            elif legs[k]["bookable"] and legs[k + 1]["bookable"]: bad.append(("NO_BUFFER_ENTRY", tag, k))
        for l in legs:
            if int((P(l["arriveAt"]) - P(l["departAt"])).total_seconds() // 60) != l["durationMin"]: bad.append(("LEG_DURATION_MISMATCH", tag, l["id"]))
        if not (0 <= o["risk"]["missProbability"] <= 1): bad.append(("RISK_RANGE", tag))
print(json.dumps(stats))
from collections import Counter
print(Counter(b[0] for b in bad))
for b in bad[:25]: print(b)
