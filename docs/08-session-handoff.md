# Session handoff — 24 Aug 2026

Stop here and resume from **T-010** pricing rule hierarchy. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-005 | Done |
| T-006 deterministic seed | Done — idempotent SUMMIT/NIMBUS catalog, 24 offers, NIMBUS cannot sell OCEANIC |
| T-007 API host | Done — tenant + correlation middleware, problem details, clocks, `/health` |
| Phase 0 | Green |
| Next | **T-010** `PricingRule` hierarchy, `RuleScope`, `Specificity`, total precedence comparator |

## Verify

- Seeding twice does not duplicate rows.
- Missing `X-Partner-Code` returns RFC 7807 with `errorCode: PARTNER_NOT_RESOLVED`.
- `GET /health` does not require a partner. API listens on **5180**.

## WDAC note (resolved)

A 4 KB unsigned `LoyaltyLab.Application.dll` stub was blocked (`0x800711C7`). Adding the real Application ports grew the assembly; it now loads. Not a machine-wide block.

## First actions next session

1. **T-010** pricing rule hierarchy and total precedence comparator (FR-P-02, FR-P-03, FR-P-04).
2. Do not start F5 unless Phases 0–5 are complete.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

Ledger opening balances, pricing rule rows, and F5 busy periods are **not** in the seed yet — those entities land with their features. Partner policies already encode burn caps and drift.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
