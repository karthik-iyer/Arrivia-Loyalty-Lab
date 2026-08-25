# Session handoff — 25 Aug 2026

Stop here and resume from **T-038** fault injection. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-035 | Done — payment, supplier, saga domain, steps, orchestrator |
| T-036 | Done — transactional outbox, dispatcher, retry, poison table |
| T-037 | Done — recovery worker, stall detection, resume |
| Next | **T-038** `FaultProfile`, `X-Fault-Profile`, production refusal |

## Verify

- A saga whose heartbeat is older than `StalledAfterSeconds` is resumed and reaches a terminal state (FR-B-11).
- A fresh heartbeat is left alone.

## First actions next session

1. **T-038** Fault injection (FR-B-09, NFR-14).
2. Then T-039 booking HTTP + operator/admin.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
