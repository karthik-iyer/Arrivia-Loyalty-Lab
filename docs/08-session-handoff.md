# Session handoff — 24 Aug 2026

Stop here and resume from **T-025** wallet and liability HTTP. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-024 | Done — ledger through derived balance, statement, liability, reconcile, FIFO expiry |
| Next | **T-025** `/wallet/balance`, `/wallet/statement`, `/reports/liability` |

## Verify

- Past-dated liability (issued 500 / burned 200 / expired 50 / outstanding 250) is unchanged after a later earn.
- Reconcile reports a booking-vs-ledger gap and does not post a correction.
- FIFO expiry lapses the oldest remaining lot and leaves younger lots.

## First actions next session

1. **T-025** wallet HTTP and finance-only liability report.
2. Then T-026 property-based invariant tests.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
