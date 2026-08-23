# Session handoff — 23 Aug 2026

Stop here and resume from **T-002 compile fix**, then **T-003**. Design is complete. Implementation has only the solution scaffold and architecture tests. F5 stays stretch.

## Where we are

| Item | Status |
|---|---|
| Docs 01–07 + 14 ADRs | Done, on `origin/main` (`cc3a4d9`) |
| Solution + projects + architecture tests | Done, on `origin/main` (`be95d3a`) |
| T-001 solution scaffold | Done |
| T-002 architecture tests | Written; **do not compile** (CA1707) |
| T-003 … T-007 (Money, tenancy, EF, seed, API host) | Not started |
| F1–F5 feature code | Not started |
| Frontend | Not started |

Remote: `https://github.com/karthik-iyer/Arrivia-Loyalty-Lab` · branch `main` · working tree was clean when this was written.

## First actions tomorrow

1. **Unblock architecture tests.** `TreatWarningsAsErrors` + CA1707 rejects xUnit names with underscores (`Domain_depends_on_no_other_layer`). Prefer `NoWarn` CA1707 on test projects in `Directory.Build.props` (xUnit convention). Then `dotnet test`.
2. **T-003** — Domain commons: `Money`, `Percent`, `Result<T>`, `Error`, `Entity<TId>`, strongly-typed ids, `IClock`. See [04 §1.1](04-detailed-design.md#11-common-building-blocks).
3. Continue Phase 0 in order: T-004 tenancy/catalog → T-005 EF + tenant filters → T-006 seed → T-007 API host.

Do not start F1 pricing until Phase 0 is green. Do not start F5 unless Phases 0–5 are complete.

## Scope we already agreed

- **Five features, one solution.** F1 pricing · F2 ledger · F3 booking saga · F4 concierge+MCP · F5 opportunity *(stretch)*.
- **Cut line** ([07 §12](07-task-breakdown.md#12-cut-line)): drop F5 first. Never cut architecture tests, raw-JSON rate-leak assertion, ledger property tests, saga crash-recovery test, prompt-injection test.
- **Day plan:** D1 Phase 0 + most of F1 · D2 finish F1 + F2 · D3 saga · D4 frontend + start F4 · D5 finish F4 + polish. Day 3 is the risk.
- Payment is a **separate process** (`LoyaltyLab.PaymentSim`). Checkout is a saga. See [ADR-0006](adr/0006-payment-out-of-process-and-saga.md).

## Constraints the next session must keep

- Domain: no project refs, no `double`/`float`, no `DateTime.Now` — only `IClock`.
- Expected failures: `Result<T>` + error catalog codes. Exceptions = defects only.
- Ledger: append-only (no update/delete on the repo). Balances derived, never stored.
- Quotes persisted and immutable. Pricing pipeline order is fixed (floor before rounding).
- Tenant isolation via EF global query filters; cross-tenant = 404, not 403.
- Architecture tests written **before** domain code so the first violation fails immediately.
- Definition of done: compile with no warnings, tests pass, design updated in the same commit if behaviour differs, commit message starts with `[T-xxx]`.

## Seed partners (do not invent others)

- **SUMMIT** — tiers, 12% markup, Gold −3%, MARCH-BEACH −5%, 40% burn cap, absorb-drift, Maya (Gold, 6000 credits) and Ravi (Standard, 500).
- **NIMBUS** — flat 18%, no tiers, 100% burn cap, requote-on-drift, Chen (12000). Cannot sell OCEANIC.

## Open compile/test note

`IsTestProject` is not set on the test `.csproj` files, so the `Directory.Build.props` test-only property group may not apply. Set `<IsTestProject>true</IsTestProject>` when fixing CA1707.

## Do not redo

Do not rewrite docs 01–07 or the ADRs unless implementation forces a same-commit design update (NFR-11). The design is the source of truth; code follows [07](07-task-breakdown.md).
