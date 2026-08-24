# Session handoff — 24 Aug 2026

Stop here and resume from **T-006 seed**, then **T-007 API host**. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| T-001 … T-004 | Done |
| T-005 EF + tenant filters | Done — query filter proven; `InitialCreate` migration generated |
| T-006 seed | Not started |
| T-007 API host | Not started |

## WDAC note (resolved)

A 4 KB unsigned `LoyaltyLab.Application.dll` stub was blocked (`0x800711C7`). Adding the real Application ports grew the assembly; it now loads. Not a machine-wide block.

## First actions next session

1. **T-006** deterministic seed (SUMMIT / NIMBUS, Maya / Ravi / Chen).
2. **T-007** tenant middleware, correlation, problem details, clocks, health.

Do not start F1 until Phase 0 is green. Do not start F5 unless Phases 0–5 are complete.

## What T-002/T-003/T-004 already enforce

- `Directory.Build.props` sets `IsTestProject` and `NoWarn` CA1707 for `*.Tests`.
- Cross-currency `Money` arithmetic throws `DomainException`.
- `ApplyPercent` does not round; `RoundToCents` is AwayFromZero.
- Member is `ITenantOwned`. `LoyaltyLabDbContext` has a global query filter on `Member.PartnerId`.
- Policies live on `Partner` (FR-X-07). Theme colours are `#RRGGBB`.

## Scope we already agreed

Five features, one solution. F5 stretch. Payment out of process. Checkout is a saga (ADR-0006). Cut line in `docs/07-task-breakdown.md` §12.

## Do not redo

Do not rewrite docs 01–07 unless implementation forces a same-commit design update (NFR-11).
