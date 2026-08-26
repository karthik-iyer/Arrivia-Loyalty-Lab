# Session handoff — 25 Aug 2026

Stop here and resume from **T-081** demo script: the numbered walkthrough from problem statement §7, including a nudge and a suppression. F5 is complete.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-040 | Done — payment through resilience |
| T-050 … T-057 | Done — Angular Phase 4 |
| T-060 … T-066 | Done — concierge through grounding / prompt-injection tests |
| T-070 | Done — TravelWindow, Nudge, BusyPeriod, PriceWatch, persistence |
| T-071 | Done — window detection, engine-priced scoring, delivered nudges |
| T-072 | Done — fatigue in order, suppressions persisted |
| T-073 | Done — PriceWatch baselines and batched scan worker |
| T-074 | Done — `/inbox`, action, and dismiss — actioning re-quotes |
| T-075 | Done — Inbox UI with the "why am I seeing this?" signal breakdown |
| T-076 | Done — opportunity suite: detection, scoring, each suppression, expiry (G15, G16) |
| T-080 | Done — README prerequisites, `scripts/run-all.ps1`, troubleshooting |
| Next | **T-081** Demo script — problem statement §7 walkthrough, verified on a clean clone |

## Verify

- Domain `OpportunitySuiteTests` prove lead/nights, weights, price-drop threshold, weekly cap, cooldown days, and lifetime are `OpportunityPolicy` (G16), and that a score is five named signals (G15).
- Evaluate: raising `ScoreThreshold` to 0.90 suppresses Maya's 0.68; raising `MaxNudgesPerMemberPerWeek` to 3 allows a send the default cap of 2 blocks. A delivered nudge plus a second scan records `DuplicateOfRecentNudge`.
- `POST /inbox/{id}/action` after the 7-day lifetime returns `NUDGE_EXPIRED` 410; GET inbox is empty.

## First actions next session

1. **T-081** Demo script — the numbered walkthrough from [problem statement §7](01-problem-statement.md), verified end to end on a clean clone (include a nudge and a suppression).
2. Then T-082 docs/code consistency and T-083 full suite / warning sweep / fresh-clone.

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).

## Progress vs remaining

**Done: 55 of 58 tasks with F5 included.** Remaining: T-081–T-083 (demo and polish).

## Scope we already agreed

Five features, one solution. **F5 is now in** (reviewer still has time). Payment out of process. Checkout is a saga (ADR-0006). Angular is Phase 4. Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
