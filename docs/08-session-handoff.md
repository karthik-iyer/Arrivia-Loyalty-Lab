# Session handoff — 24 Aug 2026

Stop here and resume from **T-030** payment simulator. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| Next | **T-030** `LoyaltyLab.PaymentSim`: authorize, capture, void, refund, query-by-key |

## Verify

- 1 000 randomized ledger sequences preserve all five invariants in `docs/02` §3.2.
- A failing seed is named so the sequence reproduces.

## First actions next session

1. **T-030** Payment simulator (out of process, idempotent, configurable faults).
2. Then T-031 `IPaymentGateway` + Http adapter.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
