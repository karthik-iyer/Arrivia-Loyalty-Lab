# Loyalty Lab

A reference implementation of the hardest problems in **white-label travel loyalty**: multi-tenant member pricing, a trustworthy rewards-currency ledger, a booking process that stays consistent when systems fail, an AI concierge that cannot recommend something the member can't actually book, and a nudge engine that knows when to stay quiet.

> **Status:** F1–F5 are implemented. Remaining polish is T-083.

## Why this exists

White-label travel loyalty is a business-to-business-to-consumer model. A technology provider negotiates private travel rates, then serves them inside *other companies'* branded portals — a bank, a mobile carrier, a hotel group. The end customer never sees the provider's name.

That model creates engineering problems a normal travel booking site never has to solve:

1. **One rate, many prices.** The same supplier room must be priced differently for every partner brand, every membership tier, and every active campaign — correctly, repeatably, and explainably. It is also contractually private, so it must never leak to an unauthenticated visitor.
2. **Points are a liability.** A rewards currency is a promise to deliver value later. It needs real double-entry accounting, exact reversal on cancellation, and a defensible number for "what do we owe members right now?"
3. **Booking spans systems that fail independently.** A payment service, a supplier, and a ledger cannot share a transaction. Every step needs a compensating action, every retry needs to be safe, and a timeout means *unknown* rather than *failed*.
4. **An AI concierge that invents inventory is a refund fight.** Recommendations must be grounded in what that specific member of that specific partner can actually book and afford.
5. **Waiting for a search wastes the relationship.** A loyalty program knows its members. The advantage is noticing an opportunity before they go looking — without training them to ignore you.

Loyalty Lab implements all five as one coherent product rather than five disconnected demos.

## The five features

| # | Feature | What it does |
|---|---|---|
| **F1** | **Pricing & margin engine** | Turns one supplier net rate into the correct member-facing price per partner, tier, and campaign — with a full "explain this price" trace and closed-user-group leak protection. |
| **F2** | **Savings Credits ledger** | Append-only double-entry ledger for the rewards currency: earn, burn, mixed cash-plus-credits payment, expiry, exact reversal on cancellation, and per-partner liability reporting. |
| **F3** | **Resilient booking saga** | Orchestrates supplier, payment, and ledger across a real process boundary with persisted state, compensations, a transactional outbox, crash recovery, and a fault-injection switch for demonstrating all of it. |
| **F4** | **Grounded concierge (+ MCP)** | Recommends only offers the member is eligible for and can afford, returns an audit block explaining inclusions and exclusions, and exposes the same capability to AI agents over the Model Context Protocol. |
| **F5** | **Opportunity engine** *(stretch, implemented)* | Detects travel windows, watches prices, scores opportunities from named signals, and — crucially — records why it chose to stay quiet. |

## Tech stack

- **Backend:** .NET 10, Minimal APIs, Clean Architecture (Domain / Application / Infrastructure / Api), EF Core + SQLite
- **Resilience:** hand-rolled saga orchestration over a persisted state machine, a transactional outbox, Polly retry policies, and a separate payment simulator process so distributed failure is real rather than mimed
- **Frontend:** Angular 21 standalone components with signals, layered as Domain / Application / Data / Feature — with ESLint boundary rules enforcing it
- **Tests:** xUnit unit, property-based, integration, and chaos tests, plus **architecture tests** that fail the build if a layer dependency rule is violated
- **AI:** deterministic recommender core with a template narrator (`IOfferNarrator` is the LLM seam), so the demo runs with no API key

## Documentation

The `docs/` folder is the design record, written before the code. Read in order:

| Doc | Contents |
|---|---|
| [01 — Problem statement](docs/01-problem-statement.md) | Business context, the five problems, goals, non-goals, personas, glossary |
| [02 — Requirements](docs/02-requirements.md) | Functional and non-functional requirements with stable ids, 14 user stories, traceability matrix |
| [03 — High-level design](docs/03-high-level-design.md) | Architectural drivers, solution structure, system context, key flows, cross-cutting design |
| [04 — Detailed design (backend)](docs/04-detailed-design.md) | Domain model, all five feature designs, application layer, persistence, error catalog, API contracts, testing strategy |
| [05 — Detailed design (frontend)](docs/05-frontend-design.md) | Angular layers, ports and adapters, signal stores, screens, theming, accessibility |
| [ADRs](docs/adr/) | 14 decision records, each with the alternatives that were rejected and why |
| [06 — Future improvements](docs/06-future-improvements.md) | Honest gaps, production roadmap, and what one more day, week, or month would buy |
| [07 — Task breakdown](docs/07-task-breakdown.md) | Task-by-task implementation plan, day plan, and a pre-agreed scope cut line |
| [08 — Session handoff](docs/08-session-handoff.md) | Where we stopped and what to do first in the next session |
| [09 — Demo script](docs/09-demo-script.md) | Numbered ten-minute walkthrough of problem statement §7 |

## Getting started

No cloud account, API key, or Docker. SQLite and seeding happen when the API starts. Development mode pins the demo clock at **15 March 2026 12:00 UTC** so March beach inventory and the worked-example prices stay stable.

### Prerequisites (bare machine)

| Tool | Version | Check |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | 10.x | `dotnet --version` |
| [Node.js](https://nodejs.org) | 22 LTS (20.19+ also works) | `node --version` |
| npm | comes with Node | `npm --version` |

Git is only needed to clone. Windows PowerShell 5.1 or PowerShell 7 can run the start script.

### One script

From the repository root:

```powershell
powershell -File scripts/run-all.ps1
```

That opens three windows (payment simulator, API, Angular), waits until each is healthy, and prints the URLs. Browse to [http://127.0.0.1:4200/](http://127.0.0.1:4200/). The demo switcher defaults to **Maya · Summit Gold**.

### Three terminals

Use this when you want logs in the current shell instead of extra windows. Run them in this order so checkout can reach the simulator.

**1 — Payment simulator** (`:5190`)

```powershell
dotnet run --project src/LoyaltyLab.PaymentSim --launch-profile http
```

**2 — API** (`:5180`)

```powershell
dotnet run --project src/LoyaltyLab.Api --launch-profile http
```

**3 — Angular** (`:4200`)

```powershell
Set-Location src/loyaltylab-web
npm install
npx ng serve --host 127.0.0.1 --port 4200
```

`--launch-profile http` sets `ASPNETCORE_ENVIRONMENT=Development` (demo clock, fault-injection switch). Do not pass `--no-launch-profile` unless you also export that environment variable.

### Tests

```powershell
dotnet test LoyaltyLab.slnx
Set-Location src/loyaltylab-web
npx ng test --watch=false
npm run lint:boundaries
```

If `dotnet test` fails because `LoyaltyLab.Api.dll` is locked, stop the running API (or pass `-p:UseArtifactsOutput=true`).

### Troubleshooting

| Symptom | Likely cause | What to do |
|---|---|---|
| `scripts/run-all.ps1` cannot find `dotnet` or `node` | SDK installed, terminal not restarted | Close and reopen the terminal, then confirm `dotnet --version` / `node --version`. |
| Port 5180, 5190, or 4200 already in use | Leftover process from a previous run | Stop that window, or `netstat -ano` and look for `:5180` / `:5190` / `:4200`. The script reuses a port if something is already listening. |
| Web loads, Offers fail, or concierge says **Not Found** | API not running, or an old API started before those endpoints existed | Restart `LoyaltyLab.Api` from current `main`. Confirm `http://localhost:5180/health`. |
| Checkout hangs or payment steps fail | PaymentSim is down | Start terminal 1 first. Confirm `http://localhost:5190/health`. |
| March beach search returns nothing | API not in Development (clock is "now") | Use `--launch-profile http` or set `ASPNETCORE_ENVIRONMENT=Development`. |
| `PARTNER_NOT_RESOLVED` from curl | Missing tenant header | Browser demo switcher sets `X-Partner-Code` / `X-Member-Id`. For curl: `-H "X-Partner-Code: SUMMIT" -H "X-Member-Id: a11ce001-0002-7000-8000-000000000001"`. |
| `dotnet test` / build: file locked | Running API holds the output DLL | Stop the API, or test with `-p:UseArtifactsOutput=true`. |
| SQLite `database is locked` | Two API processes on the same `loyaltylab.db` | Run a single API. Tests use their own temp databases. |

## Demo

The click-by-click walkthrough — two partner prices, a clamped explanation, concierge audit, mixed tender and cancel, chaos compensation, signed-out leak protection, a nudge and a suppression, then the test suite — is [09 — Demo script](docs/09-demo-script.md). Success criteria are listed in [problem statement §7](docs/01-problem-statement.md#7-success-criteria-for-the-proof-of-concept).

## A note on scope

This is a proof of concept built to explore a domain, not a production system. Deliberate limitations — a simulated supplier, hotels only, seeded partners, and **no authentication** — are stated explicitly in [Non-goals](docs/01-problem-statement.md#5-non-goals) and [The honest gaps](docs/06-future-improvements.md#1-the-honest-gaps) rather than left for the reader to discover.
