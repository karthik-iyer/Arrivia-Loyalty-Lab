# Session handoff — 24 Aug 2026

Stop here and resume from **T-031** payment gateway port and HTTP adapter. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 | Done — payment simulator (authorize / capture / void / refund / query-by-key) |
| Next | **T-031** `IPaymentGateway` port and `HttpPaymentGateway` with Polly timeout → `Unknown` |

## Verify

- Same `Idempotency-Key` twice yields one authorization at `http://localhost:5190`.
- A client timeout still leaves a queryable hold (hang is after the store).

## First actions next session

1. **T-031** `IPaymentGateway` + `HttpPaymentGateway` (timeout maps to `Unknown`, never to failure).
2. Then T-032 simulated supplier client.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
