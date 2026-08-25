# Session handoff — 25 Aug 2026

Stop here and resume from **T-057** frontend tests wrap-up. F5 stays stretch.

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
| T-055 | Done — wallet balance, statement, reversal links to original |
| T-056 | Done — operator saga list (review-needed first) and step timeline |
| Next | **T-057** frontend tests wrap-up: stores, mappers, fake ports (no HTTP mocks) |

## Verify

- Operator list sorts `RequiresManualReview` first.
- Detail shows step status, attempts, duration, error, compensation, and poison.
- The failing step is highlighted when the saga needs review.
- Component tests use a fake `OPERATOR_PORT`, not HTTP mocks.

## First actions next session

1. **T-057** Frontend tests wrap-up (NFR-09): no HTTP mock in component tests.
2. Then Phase 5 — T-060 concierge domain.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 39 of 51 core tasks.** 12 core tasks remain (T-057, T-060–T-066, T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
