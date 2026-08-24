# Session handoff — 24 Aug 2026

Stop here and resume from **T-012** price trace and role-aware projection. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 rule hierarchy + total precedence | Done |
| T-011 eight-stage pipeline | Done — both worked examples match |
| Next | **T-012** `PriceTraceEntry`, clamp recording, member projection with no net rate |

## Verify

- Distinct pricing rules never tie under the precedence comparator.
- SUMMIT Gold in March prices to **120.75** (floor clamp), max tender **48.30**.
- NIMBUS (no tiers) prices the same offer to **135.70**.

## First actions next session

1. **T-012** trace entries and role-aware projection (FR-P-07, FR-P-08).
2. Then T-013 quote persistence, T-014 use cases, T-015 HTTP + raw-JSON no-`netRate` assertion.

Do not start F5 unless Phases 0–5 are complete.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

Ledger opening balances, pricing rule rows, and F5 busy periods are **not** in the seed yet — those entities land with their features.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
