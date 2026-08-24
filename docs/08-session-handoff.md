# Session handoff — 24 Aug 2026

Stop here and resume from **T-022** idempotency store. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-021 | Done — ledger domain + append-only persistence |
| Next | **T-022** `IIdempotencyStore` unique index and payload hashing |

## Verify

- `ILedgerRepository` only has Add/Get/Find/List members (architecture test).
- Posted earn round-trips with both legs summing to zero.
- Cross-tenant ledger rows are invisible; every `ITenantOwned` root entity has a query filter.

## First actions next session

1. **T-022** idempotency store: unique `(PartnerId, Operation, Key)`, payload hash, `IDEMPOTENCY_KEY_REUSED`.
2. Then T-023 earn/burn/expire/reversal/adjustment operations.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
