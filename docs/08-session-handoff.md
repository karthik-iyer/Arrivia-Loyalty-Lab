# Session handoff — 26 Aug 2026

Stop here and resume from **T-083** full suite, warning sweep, and fresh-clone verification. T-082 is complete.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-081 | Done |
| T-082 | Done — docs aligned with code; design §12 closed as resolved |
| Next | **T-083** Full suite, warning sweep, fresh-clone verification |

## Verify (T-083)

- `dotnet test LoyaltyLab.slnx` (not `.sln`) with no warnings.
- Angular `npx ng test --watch=false` and `npm run lint:boundaries`.
- Wipe `loyaltylab.db` and walk [09 — Demo script](09-demo-script.md) on a clean clone.

## First actions next session

1. **T-083** Full suite run, warning sweep, fresh-clone verification.

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).

## Progress vs remaining

**Done: 57 of 58.** Remaining: T-083.

## Scope we already agreed

Five features, one solution. **F5 is in.** Payment out of process. Checkout is a saga (ADR-0006). Angular is Phase 4.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
