# Session handoff — 25 Aug 2026

Stop here and resume from **T-081** demo script (problem statement §7 walkthrough). F5 stays stretch.

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
| T-060 … T-066 | Done — concierge through grounding / prompt-injection tests |
| T-080 | Done — README prerequisites, `scripts/run-all.ps1`, troubleshooting |
| Next | **T-081** Demo script from problem statement §7 |

## Verify

- `README.md` lists .NET 10 and Node 22, one-script start, three-terminal start, and troubleshooting.
- `powershell -File scripts/run-all.ps1` brings up PaymentSim `:5190`, API `:5180`, and Angular `:4200`.

## First actions next session

1. **T-081** Numbered walkthrough from [problem statement §7](01-problem-statement.md), verified end to end.
2. Then T-082 consistency pass, T-083 full suite / fresh clone.

Do not start F5 unless Phases 0–5 are complete. Angular is Phase 4 — not per-feature.

## Progress vs remaining

**Done: 48 of 51 core tasks.** 3 core tasks remain (T-081–T-083). F5 (T-070–T-076) stays stretch.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
