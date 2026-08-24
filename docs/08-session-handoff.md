# Session handoff — 24 Aug 2026

Stop here and resume from **T-023** ledger operations. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-022 | Done — ledger domain, append-only persistence, idempotency store |
| Next | **T-023** Earn, burn, expire, reversal, adjustment with burn-cap |

## Verify

- Concurrent same-key inserts produce one `IdempotencyRecords` row.
- Same key + different payload returns `IDEMPOTENCY_KEY_REUSED`.
- Every `ITenantOwned` root entity still has a query filter.

## First actions next session

1. **T-023** earn/burn/expire/reversal/adjustment operations with burn-cap and balance checks; reversal restores exact original amounts.
2. Then T-024 balances, statement, liability, reconcile, expire worker.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
