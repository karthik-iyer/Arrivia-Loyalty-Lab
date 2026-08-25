# Session handoff — 24 Aug 2026

Stop here and resume from **T-036** transactional outbox. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-035 | Done — payment, supplier, saga domain, steps, orchestrator |
| Next | **T-036** transactional outbox, dispatcher worker, retry, poison table |

## Verify

- Each row of `docs/02-requirements.md` §4.3 has an orchestrator test.
- Transient execute retries with backoff; catalog business failures compensate immediately.
- Exhausted compensation terminates `RequiresManualReview`.

## First actions next session

1. **T-036** Transactional outbox (FR-B-06, FR-B-07).
2. Then T-037 recovery worker.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
