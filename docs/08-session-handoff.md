# Session handoff — 25 Aug 2026

Stop here and resume from **T-039** booking HTTP + operator/admin. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-007 | Done — Phase 0 green |
| T-010 … T-016 | Done — Phase 1 pricing complete |
| T-020 … T-026 | Done — ledger through property-based invariants |
| T-030 … T-037 | Done — payment, supplier, saga, outbox, recovery |
| T-038 | Done — `FaultProfile`, `X-Fault-Profile`, production refusal |
| Next | **T-039** `POST /bookings`, operator sagas, admin workers |

## Verify

- API refuses to start with `Features:FaultInjection=true` in Production (NFR-14).
- Development with the flag on starts and registers the injector.
- `X-Fault-Profile` drives supplier hooks; `CrashAfterStep` throws after persist.

## First actions next session

1. **T-039** booking HTTP + operator/admin (FR-B-08).
2. Then T-040 resilience tests against the real simulator.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 30 of 51 core tasks** (Phases 0–2 complete; Phase 3 through T-038). F5 (T-070–T-076) is stretch and is not started.

| Phase | Status | Left |
|---|---|---|
| 0 Foundation | T-001–T-007 done | — |
| 1 Pricing | T-010–T-016 done | — |
| 2 Ledger | T-020–T-026 done | — |
| 3 Booking saga | T-030–T-038 done | **T-039** HTTP + operator/admin, **T-040** kill/recover vs PaymentSim |
| 4 Angular | not started | T-050–T-057 (8) |
| 5 Concierge + MCP | not started | T-060–T-066 (7) |
| 7 Polish | not started | T-080–T-083 (4) |
| 6 Opportunity (stretch) | do not start until 0–5 are green | T-070–T-076 (7) |

**21 core tasks remain** after this commit. The booking *engine* is in place; T-039 is the first time a client can start a booking over HTTP. Angular is Phase 4 — not per-feature. F5 stays cuttable per `docs/07` §12.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
