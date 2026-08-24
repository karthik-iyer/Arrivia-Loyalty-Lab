# Session handoff — 24 Aug 2026

Stop here and resume from **T-021** ledger persistence. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 ledger domain | Done — four accounts, balanced factory, five posting types |
| Next | **T-021** `ILedgerRepository` append+read only, EF, architecture test |

## Verify

- Unbalanced `LedgerTransaction.Create` throws `LEDGER_UNBALANCED`.
- Earn 500 / burn 200 / expire 50 leaves member **250**, issuance **−500**, redemption **200**, breakage **50**, books net zero.
- Reversal mirrors the original legs; a reversal cannot be reversed.

## First actions next session

1. **T-021** `ILedgerRepository` with **no update or delete member**, EF, persistence, architecture test.
2. Then T-022 idempotency store.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
