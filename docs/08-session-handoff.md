# Session handoff — 25 Aug 2026

Stop here and resume from **T-050** Angular 21 scaffold. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-038 | Done — payment, supplier, saga, outbox, recovery, fault injection |
| T-039 | Done — booking HTTP, operator sagas, admin workers |
| T-040 | Done — Resilience.Tests against real PaymentSim (decline, capture fail, kill/recover, timeout, exhausted compensation) |
| Next | **T-050** Angular 21 scaffold, five layer folders, ESLint boundary rules |

## Verify

- After a kill and recovery, exactly one authorization exists at PaymentSim (NFR-13).
- Payment decline releases the reservation; capture failure reverses the burn; timeout resolves via query; exhausted compensation is `RequiresManualReview`.

## First actions next session

1. **T-050** Angular 21 scaffold, the five layer folders, and ESLint boundary rules (NFR-09).
2. Then T-051 domain models and HTTP adapters.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 32 of 51 core tasks.** 19 core tasks remain (T-050–T-057, T-060–T-066, T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
