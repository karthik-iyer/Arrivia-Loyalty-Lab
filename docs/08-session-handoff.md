# Session handoff — 25 Aug 2026

Stop here and resume from **T-065** Concierge UI with the collapsible audit disclosure. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-038 | Done — payment, supplier, saga, outbox, recovery, fault injection |
| T-039 | Done — booking HTTP, operator sagas, admin workers |
| T-040 | Done — Resilience.Tests against real PaymentSim |
| T-050 … T-057 | Done — Angular Phase 4 |
| T-060 | Done — concierge parser, pipeline, affordability, weighted ranking |
| T-061 | Done — RecommendationAudit; every exclusion has a reason |
| T-062 | Done — narrator port, template default, invented prices fall back |
| T-063 | Done — `POST /concierge/recommend` wires parser, quotes, pipeline, narrator |
| T-064 | Done — MCP tools over Recommend, ExplainQuote, GetBalance |
| Next | **T-065** Concierge UI with the collapsible audit disclosure |

## Verify

- `GET`/`POST` `/mcp` lists `get_travel_recommendations`, `explain_offer_price`, `get_credit_balance`.
- Tool JSON matches REST for the same partner, member, and input (quote ids may differ on recommend because each call issues fresh quotes).
- Types under `Api/Mcp` do not reference Domain or Application and contain no `if`/`switch`.

## First actions next session

1. **T-065** Angular concierge UI with collapsible audit (FR-C-05).
2. Then T-066 grounding / prompt-injection tests.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 45 of 51 core tasks.** 6 core tasks remain (T-065–T-066, T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
