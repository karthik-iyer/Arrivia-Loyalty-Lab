# Session handoff — 24 Aug 2026

Stop here and resume from **T-016** remaining pricing suite. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-015 | Done — rules, pipeline, traces, quotes, use cases, HTTP |
| Next | **T-016** pricing test suite (precedence, floor, rounding-once, effective dating, two-partner divergence) |

## Verify

- `GET /api/offers?stayDate=2026-03-15` with `X-Partner-Code: SUMMIT` lists Coral Bay; anonymous JSON has no `netRate` and no `memberPrice`.
- Maya (`X-Member-Id` = seed Maya) search/quote of Coral Bay is **120.75** / max credits **4830**.
- NIMBUS cannot see or quote OCEANIC (`OFFER_NOT_ELIGIBLE` / 422). Cross-partner explain is `QUOTE_NOT_FOUND` / 404.

## First actions next session

1. **T-016** extra pricing suite, then Phase 2 ledger (**T-020**).
2. Do not start Angular until Phase 4.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
