# 09 — Demo script

| | |
|---|---|
| **Document** | Numbered reviewer walkthrough |
| **Status** | Ready |
| **Satisfies** | [Problem statement §7](01-problem-statement.md#7-success-criteria-for-the-proof-of-concept), FR-X-09, NFR-12 |
| **Prerequisite** | [README — Getting started](../README.md#getting-started) |

A reviewer can finish this in about ten minutes on a machine that already has the [prerequisites](../README.md#prerequisites-bare-machine). Expected numbers are fixed: Development pins the clock at **15 March 2026 12:00 UTC**, and seed ids never change.

---

## 0. Start

From the repository root:

```powershell
powershell -File scripts/run-all.ps1
```

Browse to [http://127.0.0.1:4200/](http://127.0.0.1:4200/). The demo switcher defaults to **Maya · Summit Gold**.

**Clean database (first scan must be empty).** Stop the API window, delete `loyaltylab.db`, `loyaltylab.db-wal`, and `loyaltylab.db-shm` from the directory you started the API in (repository root when using `run-all.ps1`), then start the API again. Seeding is idempotent; leftover nudges from an earlier walkthrough are not.

Stay date in the catalog is **15 March 2026**. Do not change it.

---

## 1. The same offer, two partner prices

Coral Bay is Oceanic inventory. Nimbus cannot sell Oceanic, so the two-brand comparison uses a shared Alpine hotel.

1. Stay on **Maya · Summit Gold**. Offers → destination **Zermatt** (or tag **Ski**) → **Matterhorn Lodge**.
2. Member price **$219.45**.
3. Switch to **Chen · Nimbus**. Open Matterhorn Lodge again.
4. Member price **$238.36**. The chrome restyles without a reload.

*Why these numbers:* net $180 + tax $22 = $202. Summit Gold applies +12% then −3% Gold. Nimbus applies a flat +18% and has no Gold rule.

**Curl**

```powershell
$maya = @{ 'X-Partner-Code' = 'SUMMIT'; 'X-Member-Id' = 'a11ce001-0002-7000-8000-000000000001' }
$chen = @{ 'X-Partner-Code' = 'NIMBUS'; 'X-Member-Id' = 'a11ce001-0002-7000-8000-000000000003' }
$offer = 'a11ce001-0004-7000-8000-000000000009'
$body = '{"stayDate":"2026-03-15"}'
Invoke-RestMethod "http://localhost:5180/api/offers/$offer/quote" -Method POST -Headers $maya -ContentType 'application/json' -Body $body
Invoke-RestMethod "http://localhost:5180/api/offers/$offer/quote" -Method POST -Headers $chen -ContentType 'application/json' -Body $body
```

---

## 2. Explain a price the margin floor clamped

1. Switch back to **Maya · Summit Gold**.
2. Offers → destination **Montego Bay** → **Coral Bay Resort**.
3. Member price **$120.75**. Open **Why this price**.
4. The **Partner minimum margin** row is visually marked clamped. Without the floor the stacked Gold and MARCH-BEACH discounts would have sold at $118.69; the guardrail raised it to $120.75.

**Curl**

```powershell
$quote = Invoke-RestMethod 'http://localhost:5180/api/offers/a11ce001-0004-7000-8000-000000000001/quote' -Method POST -Headers $maya -ContentType 'application/json' -Body $body
Invoke-RestMethod "http://localhost:5180/api/quotes/$($quote.quoteId)/explain" -Headers $maya
```

The JSON has no `netRate`. That is the leak-protection claim, not an omission.

---

## 3. Concierge recommendation with an audit

1. Still Maya. **Concierge**.
2. Leave the default query `beach in Montego Bay in March` (or type it) → **Find stays**.
3. Coral Bay appears as a live quote at **$120.75**, not a guessed sentence.
4. Open **Why these results**. The audit lists candidates considered, ranking weights, and every exclusion (availability, tag mismatch, supplier not permitted, unaffordable) with a reason.

---

## 4. Mixed cash and credits, then cancel

Maya starts with **6 000** credits. Coral's burn cap is 40%, so the slider max is **4 830** credits and cash is still due — that *is* a mixed tender.

1. From Coral Bay, **Continue to checkout**. Leave the slider at the max (or drag it so both credits and cash are non-zero).
2. **Book**. The saga timeline reaches confirmed. **Booking confirmed.**
3. **Cancel booking**. The page states the booking was cancelled and links to the wallet.
4. **Wallet**. Balance is **6 000** credits again. The statement has a reversal row with **Reverses original**.

**Curl** (after quoting)

```powershell
$key = [guid]::NewGuid().ToString()
$booked = Invoke-RestMethod 'http://localhost:5180/api/bookings' -Method POST -Headers ($maya + @{ 'Idempotency-Key' = $key }) -ContentType 'application/json' -Body (@{ quoteId = $quote.quoteId; credits = 4830; stayDate = '2026-03-15' } | ConvertTo-Json)
Invoke-RestMethod "http://localhost:5180/api/bookings/$($booked.bookingId)/cancel" -Method POST -Headers ($maya + @{ 'Idempotency-Key' = [guid]::NewGuid().ToString() })
Invoke-RestMethod 'http://localhost:5180/api/wallet/balance' -Headers $maya
```

---

## 5. Chaos switch: fail mid-booking and watch compensation

The checkout checkbox is the chaos switch. It sends `X-Fault-Profile: {"paymentDecline":true}` so authorization fails after the supplier reserve. A reviewer who wants a *supplier* reserve failure can POST the same booking with `{"supplierDecline":true}` instead.

1. Maya, Coral Bay again (or Matterhorn), **Continue to checkout**.
2. Check **Demonstrate payment decline** → **Book**.
3. The timeline unwinds in reverse. The page states **Nothing was charged. Your credits are unchanged.**
4. Switch to **Operator · Summit** → **Operator**. Filter **Compensated** (or All). Open the saga. Each compensated step shows the attempt and the compensating action.

Wallet still reads 6 000 credits.

---

## 6. Signed out, member price refused

1. Switch to **Anonymous · Summit**.
2. Offers: Coral Bay shows **Sign in to see member price**.
3. Open the card: **Sign in to see member price and a price explanation.** There is no quote and no **Why this price**.

**Curl**

```powershell
Invoke-RestMethod 'http://localhost:5180/api/offers?stayDate=2026-03-15' -Headers @{ 'X-Partner-Code' = 'SUMMIT' }
# Body lists Coral Bay. There is no netRate; memberPrice is null.
Invoke-WebRequest 'http://localhost:5180/api/offers/a11ce001-0004-7000-8000-000000000001/quote' -Method POST -Headers @{ 'X-Partner-Code' = 'SUMMIT' } -ContentType 'application/json' -Body '{"stayDate":"2026-03-15"}'
# 404 OFFER_NOT_FOUND — anonymous callers are not told the offer exists as a member rate.
```

---

## 7. Opportunity scan: a nudge, then a suppression

Hosted scanning is off. The operator button runs the same use case as `POST /api/admin/run/scan`.

1. Switch to **Operator · Summit** → **Operator** → **Run opportunity scan**. The page reports members scanned (Maya is the seeded calendar).
2. Switch to **Maya · Summit Gold** → **Inbox**.
3. One card: **Coral Bay Resort**, window **29 Mar 2026 – 12 Apr 2026**, score at least 55% (typically **77% fit**). Open **Why am I seeing this?** — five named signals (window fit, destination affinity, tag affinity, credit coverage, price drop).
4. Switch back to **Operator · Summit** → **Run opportunity scan** again.
5. Maya's inbox still has **one** live nudge. The second evaluation did not spam her.
6. In the API window, an Information line records the silence, for example `Opportunity suppressed for Maya … DuplicateOfRecentNudge`.

Do not **Book this stay** or **Dismiss** before the second scan; those change fatigue state.

**Curl**

```powershell
$op = @{ 'X-Partner-Code' = 'SUMMIT'; 'X-Access-Role' = 'Operator' }
Invoke-RestMethod 'http://localhost:5180/api/admin/run/scan' -Method POST -Headers $op
Invoke-RestMethod 'http://localhost:5180/api/inbox' -Headers $maya
Invoke-RestMethod 'http://localhost:5180/api/admin/run/scan' -Method POST -Headers $op
Invoke-RestMethod 'http://localhost:5180/api/inbox' -Headers $maya
# Still one nudge. The API log names DuplicateOfRecentNudge.
```

---

## 8. Test suite, including architecture rules

Stop the running API first if `dotnet test` reports a locked `LoyaltyLab.Api.dll`.

```powershell
dotnet test LoyaltyLab.slnx
Set-Location src/loyaltylab-web
npx ng test --watch=false
npm run lint:boundaries
```

Architecture tests live in `tests/LoyaltyLab.Architecture.Tests` and run with the rest of `dotnet test`. A layer violation fails the build.

---

## Identities (seed)

| Switcher | Partner | Member id |
|---|---|---|
| Maya · Summit Gold | SUMMIT | `a11ce001-0002-7000-8000-000000000001` |
| Chen · Nimbus | NIMBUS | `a11ce001-0002-7000-8000-000000000003` |
| Anonymous · Summit | SUMMIT | (omit `X-Member-Id`) |
| Operator · Summit | SUMMIT | (omit member; `X-Access-Role: Operator`) |

| Offer | Id |
|---|---|
| Coral Bay Resort | `a11ce001-0004-7000-8000-000000000001` |
| Matterhorn Lodge | `a11ce001-0004-7000-8000-000000000009` |

**Back to:** [README](../README.md) · [Documentation index](.)
