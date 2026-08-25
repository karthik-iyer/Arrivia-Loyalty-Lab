# Session handoff — 25 Aug 2026

Stop here and resume from **T-071** window detection and signal scoring. F5 is in scope.

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
| T-080 | Done — README prerequisites, `scripts/run-all.ps1`, troubleshooting |
| Next | **T-071** Window detection and deterministic signal scoring |

## Verify

- A delivered nudge round-trips its signals and a score equal to Σ(Normalized × Weight).
- A suppressed nudge is persisted with a reason (not dropped).
- Maya has two seeded busy periods leaving a 14-night gap from 29 Mar 2026.

## First actions next session

1. **T-071** Detect windows from busy periods; score via the normal pricing engine (FR-O-01, FR-O-02, FR-O-04).
2. Then T-072 fatigue, T-073 price watch, T-074–T-076 inbox and tests.
3. Then polish T-081–T-083 so the demo walkthrough can include a nudge and a suppression.

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).

## Progress vs remaining

**Done: 49 of 58 tasks with F5 included.** Remaining: T-071–T-076 (opportunity) then T-081–T-083 (demo and polish).

## Scope we already agreed

Five features, one solution. **F5 is now in** (reviewer still has time). Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
