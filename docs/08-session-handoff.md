# Session handoff — 24 Aug 2026

Stop here and resume from **T-024** balances, statement, liability, reconcile, expire worker. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-023 | Done — ledger domain, persistence, idempotency, posting operations |
| Next | **T-024** GetBalance, GetStatement, GetLiabilityReport, ReconcileLedger, ExpireCredits worker |

## Verify

- Reversal of a burn restores the exact original legs and the pre-burn member balance.
- Burn above the 40% SUMMIT cap on 120.75 (`4831` credits) is `BURN_CAP_EXCEEDED`; `4830` is accepted.
- Expire is an explicit posting; overdrafts return `INSUFFICIENT_CREDITS`.

## First actions next session

1. **T-024** balances, statement, past-dated liability, reconcile (report, never auto-correct), FIFO expire worker using `CreditLifetimeDays`.
2. Then T-025 wallet and liability HTTP.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
