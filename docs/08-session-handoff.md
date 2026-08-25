# Session handoff — 25 Aug 2026

Stop here and resume from **T-080** README and demo runbook. F5 stays stretch.

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
| T-065 | Done — Concierge UI with collapsible audit disclosure |
| T-066 | Done — Grounding and prompt-injection tests |
| Next | **T-080** README: prerequisites, `scripts/run-all.ps1`, troubleshooting |

## Verify

- Summit jailbreak prompt does not leak Nimbus members, Nimbus prices, or `netRate`.
- Nimbus jailbreak does not recommend Oceanic inventory or leak Summit members.
- Foreign member under the wrong partner is 404 `MEMBER_NOT_FOUND`, not 403.
- Invented/jailbreak narration falls back to the template.

## First actions next session

1. **T-080** README from a bare machine: prerequisites, `scripts/run-all.ps1`, three-terminal alternative, troubleshooting.
2. Then T-081 demo script, T-082 consistency pass, T-083 full suite / fresh clone.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 47 of 51 core tasks.** 4 core tasks remain (T-080–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
