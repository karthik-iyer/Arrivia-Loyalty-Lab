# Session handoff — 25 Aug 2026

Stop here and resume from **T-076** opportunity tests: detection, scoring, each suppression reason, expiry. F5 is in scope.

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
| T-080 | Done — README prerequisites, `scripts/run-all.ps1`, troubleshooting |
| Next | **T-076** Tests: detection, scoring, each suppression reason, expiry |

## Verify

- Inbox cards show the offer, travel window, and a collapsed **"Why am I seeing this?"** listing each signal's weight and contribution.
- **Book this stay** calls `POST /inbox/{id}/action` and navigates to `/checkout/{quoteId}` with a live quote.
- **Dismiss** removes the card. `NUDGE_EXPIRED` fades the card with "This offer has expired".

## First actions next session

1. **T-076** Tests: detection, scoring, each suppression reason, expiry (G15, G16).
2. Then polish T-081–T-083 so the demo walkthrough can include a nudge and a suppression.

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).

## Progress vs remaining

**Done: 54 of 58 tasks with F5 included.** Remaining: T-076 (opportunity tests) then T-081–T-083 (demo and polish).

## Scope we already agreed

Five features, one solution. **F5 is now in** (reviewer still has time). Payment out of process. Checkout is a saga (ADR-0006). Angular is Phase 4. Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
