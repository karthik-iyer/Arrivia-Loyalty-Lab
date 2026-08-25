# Session handoff — 25 Aug 2026

Stop here and resume from **T-051** domain models, port tokens, HTTP adapters, and `provideDataLayer()`. F5 stays stretch.

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
| Next | **T-051** domain models and port tokens, HTTP adapters, mappers, `provideDataLayer()` |

## Verify

- `npm run lint:boundaries` in `src/loyaltylab-web` fails lint when `features/` imports `data/` (NFR-09).

## First actions next session

1. **T-051** Domain models and port tokens, HTTP adapters, mappers, `provideDataLayer()`.
2. Then T-052 core interceptors, session, theming.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 33 of 51 core tasks.** 18 core tasks remain (T-051–T-057, T-060–T-066, T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
