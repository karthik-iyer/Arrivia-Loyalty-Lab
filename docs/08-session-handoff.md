# Session handoff — 25 Aug 2026

Stop here and resume from **T-064** MCP server with three tools. F5 stays stretch.

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
| Next | **T-064** MCP server with three tools over the same use cases |

## Verify

- Maya `POST /api/concierge/recommend` with "beach in Montego Bay in March" returns Coral Bay at $120.75 and a real `quoteId` that `GET /quotes/{id}/explain` honours.
- `narrationApplied` is false with `NullOfferNarrator`; raw JSON contains no `netRate`.
- Nimbus records Coral Bay as `SupplierNotPermitted` and does not recommend it.
- Anonymous recommend is `MEMBER_NOT_FOUND`.

## First actions next session

1. **T-064** MCP server: `get_travel_recommendations`, `explain_offer_price`, `get_credit_balance`. Architecture test: `Api/Mcp` must not reference Domain or hold business logic. Tool and REST results agree for identical input.
2. Then T-065 Angular concierge UI.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 44 of 51 core tasks.** 7 core tasks remain (T-064–T-066, T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
