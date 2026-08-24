# Session handoff — 24 Aug 2026

Stop here and resume from **T-014** pricing use cases. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-011 | Done — rules + eight-stage pipeline |
| T-012 price trace + role-aware projection | Done — member view has no net rate |
| T-013 Quote + expiry + rate drift | Done — persisted with trace |
| Next | **T-014** `SearchOffers`, `QuoteOffer`, `ExplainQuote` |

## Verify

- Member/anonymous trace omits base-cost and net cost; internal roles see net cost, margin, and the clamp.
- Expired quotes return `QUOTE_EXPIRED`. Drift beyond tolerance or a broken floor returns `RATE_CHANGED`. Absorb-within-2% still holds the NIMBUS floor.

## First actions next session

1. **T-014** SearchOffers / QuoteOffer / ExplainQuote with fake ports.
2. **T-015** HTTP + raw-JSON assertion that anonymous responses contain no `netRate`.
3. **T-016** remaining pricing suite, then Phase 2 ledger.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
