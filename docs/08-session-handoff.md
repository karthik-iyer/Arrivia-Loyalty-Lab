# Session handoff — 25 Aug 2026

Stop here and resume from **T-055** wallet: balance and statement with reversal links. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-038 | Done — payment, supplier, saga, outbox, recovery, fault injection |
| T-039 | Done — booking HTTP, operator sagas, admin workers |
| T-040 | Done — Resilience.Tests against real PaymentSim |
| T-050 | Done — Angular 21 scaffold, layer folders, ESLint boundaries |
| T-051 | Done — domain models, port tokens, HTTP adapters, `provideDataLayer()` |
| T-052 | Done — session, tenant/correlation interceptors, theming, demo switcher |
| T-053 | Done — catalog, offer detail, price explanation panel |
| T-054 | Done — checkout tender slider, idempotency key, saga timeline |
| Next | **T-055** wallet: balance and statement with reversal links |

## Verify

- Checkout slider cannot exceed `maxCredits`.
- A compensated saga renders “Nothing was charged. Your credits are unchanged.”
- One idempotency key is reused across retries of the same attempt.
- Development enables `Features:FaultInjection` so the demo decline checkbox can send `X-Fault-Profile`.

## First actions next session

1. **T-055** Wallet: balance and statement with reversal links.
2. Then T-056 operator sagas.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 37 of 51 core tasks.** 14 core tasks remain (T-055–T-057, T-060–T-066, T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
