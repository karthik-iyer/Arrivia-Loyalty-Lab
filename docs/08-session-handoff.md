# Session handoff — 24 Aug 2026

Stop here and resume from **T-026** property-based ledger invariants. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-025 | Done — ledger through wallet HTTP and finance liability report |
| Next | **T-026** Property-based tests over randomized sequences (five invariants) |

## Verify

- Maya's wallet is the seeded 6 000 credits (`$60.00`, 40% burn cap).
- `GET /reports/liability` without `X-Access-Role: FinanceAnalyst` is `ROLE_NOT_PERMITTED` (403).
- SUMMIT finance outstanding as of the demo date is 6 500 (Maya + Ravi).

## First actions next session

1. **T-026** property-based tests asserting the five ledger invariants over 1 000 generated sequences.
2. Then Phase 3 (payment simulator, T-030).

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
