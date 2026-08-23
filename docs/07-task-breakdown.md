# 07 — Task Breakdown

| | |
|---|---|
| **Document** | Implementation plan |
| **Status** | Ready to execute |
| **Prerequisite reading** | [04 — Backend design](04-detailed-design.md), [05 — Frontend design](05-frontend-design.md) |
| **Budget** | 5 days |

Tasks are executed in order. Each is small enough to finish and verify in one sitting, and each names the requirement it satisfies — nothing gets built that is not traceable to [02 — Requirements](02-requirements.md).

**Scope commitment:** F1–F4 plus the foundation and frontend are **core**. F5 is an explicit **stretch**. The [cut line](#12-cut-line) states exactly what is dropped, and in what order, if time runs short — decided now, while it is a design choice, rather than on day five when it would be a panic.

---

## 1. Definition of done

A task is complete when all of the following hold. This is not aspirational; a task that fails any line is still in progress.

1. The code compiles with no warnings.
2. Tests named in the task pass, and the full suite still passes.
3. Architecture tests pass.
4. The behaviour matches the design document; where it deviates, **the document is updated in the same commit** (NFR-11).
5. No `TODO` comment is left standing without a task id.
6. The commit message references the task id.

---

## 2. Phase 0 — Foundation *(core)*

Nothing demonstrable ships in this phase, and skipping it would cost more than it saves.

| Task | Description | Satisfies | Verify |
|---|---|---|---|
| **T-001** | Solution and five projects with reference directions set. `Directory.Build.props` enabling nullable, implicit usings, warnings-as-errors. | NFR-01 | `dotnet build` clean |
| **T-002** | **Architecture tests first.** Domain references nothing; no `double` in Domain; no `DateTime.Now` anywhere; inward-only dependencies. | NFR-01 | Tests pass, and fail when deliberately violated |
| **T-003** | Domain commons: `Money`, `Percent`, `Result<T>`, `Error`, `Entity<TId>`, strongly-typed ids, `IClock`. | FR-X-06 | Unit tests incl. currency-mismatch throw |
| **T-004** | Tenancy and catalog: `Partner` with all four policy records, `Member`, `TenantContext`, `Supplier`, `TravelOffer`. | FR-X-01, FR-X-07 | Unit tests |
| **T-005** | EF Core `DbContext`, configurations, decimal and `DateTimeOffset` conversions, **global tenant query filters**, initial migration. | FR-X-02 | Migration applies; filter test proves cross-tenant invisibility |
| **T-006** | Deterministic seed data per [design §8.3](04-detailed-design.md#83-seed-data). | NFR-12, FR-X-09 | Seeding twice is idempotent |
| **T-007** | API host: tenant middleware, correlation middleware, problem-details handler, `SystemClock`/`FixedDemoClock`, health endpoint. | FR-X-01, FR-X-08 | Missing partner header returns `PARTNER_NOT_RESOLVED` |

**T-002 is deliberately second.** Writing architecture tests before there is an architecture to test means the first violation fails immediately, rather than being discovered after fifty files have grown to depend on it.

---

## 3. Phase 1 — F1 Pricing engine *(core)*

| Task | Description | Satisfies | Verify |
|---|---|---|---|
| **T-010** | `PricingRule` hierarchy, `RuleScope`, `Specificity`, and the **total** precedence comparator. | FR-P-02, FR-P-03, FR-P-04 | Test proves ordering is total — no input pair ties |
| **T-011** | The eight pipeline stages in order, with `PricingState` and short-circuit on ineligibility. | FR-P-01, FR-P-05, FR-P-10 | Both [worked examples](04-detailed-design.md#24-worked-examples) reproduce exactly |
| **T-012** | `PriceTraceEntry`, clamp recording, role-aware trace projection. | FR-P-07, FR-P-08 | Member projection contains no net rate |
| **T-013** | `Quote` entity, persistence, expiry, `RateDriftPolicy` evaluation. | FR-P-06, FR-P-09, FR-P-11 | Expired quote rejected; drift absorbed within tolerance, rejected beyond |
| **T-014** | `SearchOffers`, `QuoteOffer`, `ExplainQuote` use cases. | FR-P-01, FR-P-07 | Use case tests with fake ports |
| **T-015** | `GET /offers`, `POST /offers/{id}/quote`, `GET /quotes/{id}/explain`. | FR-X-05 | **Raw JSON** assertion that anonymous responses contain no `netRate` |
| **T-016** | Pricing test suite: precedence, floor clamping, rounding-once, effective dating, two-partner divergence. | G1, G2, G3 | All pass |

**Milestone:** the same offer prices differently for SUMMIT and NIMBUS, with a full explanation, and the net rate is provably absent from member-facing payloads.

---

## 4. Phase 2 — F2 Savings Credits ledger *(core)*

| Task | Description | Satisfies | Verify |
|---|---|---|---|
| **T-020** | Ledger domain: four account types, `LedgerTransaction` factory asserting balance, entry types. | FR-L-01, FR-L-02, FR-L-03 | Unbalanced construction throws |
| **T-021** | `ILedgerRepository` with **no update or delete member**, EF implementation, persistence. | FR-L-01 | Architecture test asserts the interface exposes no mutating method |
| **T-022** | `IIdempotencyStore` with unique index and payload hashing. | FR-L-05 | Concurrent same-key requests produce one effect; different payload returns `IDEMPOTENCY_KEY_REUSED` |
| **T-023** | Earn, burn, expire, reversal, adjustment operations with burn-cap and balance checks. | FR-L-06, FR-L-08, FR-L-09 | Reversal restores exact original amounts |
| **T-024** | `GetBalance`, `GetStatement`, `GetLiabilityReport`, `ReconcileLedger`, `ExpireCredits`. | FR-L-04, FR-L-10 … FR-L-12 | Past-dated report is stable under later activity |
| **T-025** | `/wallet/balance`, `/wallet/statement`, `/reports/liability`. | FR-L-10 | Finance role required for the report |
| **T-026** | **Property-based tests** over randomized transaction sequences asserting all five [invariants](02-requirements.md#32-invariants). | G4, G6 | 1 000 generated cases pass |

**Milestone:** credits earn, burn, expire, and reverse exactly; liability reconciles; invariants hold under randomized input.

---

## 5. Phase 3 — F3 Booking saga *(core)*

The largest phase, and the one that most rewards being built in this order — the simulator first, so every later step has something real to fail against.

| Task | Description | Satisfies | Verify |
|---|---|---|---|
| **T-030** | `LoyaltyLab.PaymentSim`: authorize, capture, void, refund, query-by-key. Configurable latency, decline rate, and timeout. Idempotency-key aware. | [ADR-0006](adr/0006-payment-out-of-process-and-saga.md) | Same key twice yields one authorization |
| **T-031** | `IPaymentGateway` port and `HttpPaymentGateway` adapter with Polly timeout, retry, and backoff. Timeout maps to `Unknown`, never to failure. | FR-B-03, FR-B-04 | Forced timeout produces `StepResult.Unknown` |
| **T-032** | `SimulatedSupplierClient`: reserve, release, query-by-key, with fault hooks. | FR-B-04 | Query resolves an ambiguous reservation |
| **T-033** | Saga domain: `SagaInstance`, `SagaStepRecord`, statuses, derived idempotency keys, persistence with unique index and version. | FR-B-02, FR-B-12 | Two sagas for one booking is impossible |
| **T-034** | Six `ISagaStep` implementations, each with execute, compensate, and resolve-unknown. | FR-B-01 | Unit test per step, all three paths |
| **T-035** | Orchestrator: advance loop, retry with backoff, reverse-order compensation, terminal states. | FR-B-01, FR-B-05, FR-B-10 | Each row of [failure semantics §4.3](02-requirements.md#43-failure-semantics) has a test |
| **T-036** | Transactional outbox, dispatcher worker, retry, poison table. | FR-B-06, FR-B-07 | Killing the process after commit still delivers the event |
| **T-037** | Recovery worker with heartbeat and stall detection. | FR-B-11 | Stalled saga reaches a terminal state |
| **T-038** | `FaultProfile`, `X-Fault-Profile` header, config gate, production refusal. | FR-B-09, NFR-14 | API refuses to start with the flag on in production |
| **T-039** | `POST /bookings`, `GET /bookings/{id}`, `POST /bookings/{id}/cancel`, `/operator/sagas`, `/admin/run/{worker}`. | FR-B-08 | Operator payload shows steps, attempts, compensations |
| **T-040** | `Resilience.Tests` against the real simulator: decline, capture failure, mid-saga kill and recover, timeout resolution, exhausted compensation. | G12, G13, NFR-13 | After a kill and recovery, **exactly one** authorization exists at the simulator |

**Milestone:** a booking can be broken at any step and always lands in a consistent, explicable terminal state.

---

## 6. Phase 4 — Frontend core *(core)*

| Task | Description | Satisfies | Verify |
|---|---|---|---|
| **T-050** | Angular 21 scaffold, the five layer folders, and **ESLint boundary rules**. | NFR-09 | An import from `features/` to `data/` fails lint |
| **T-051** | Domain models and port tokens, HTTP adapters, mappers, `provideDataLayer()`. | NFR-09 | Mapper tests against captured payloads |
| **T-052** | Core: tenant and correlation interceptors, session signal, `ProblemDetailsMapper`, theming effect, demo switcher. | FR-X-04, FR-X-08 | Switching partner restyles without reload |
| **T-053** | Catalog and offer detail with the price explanation panel. | FR-P-07, FR-P-08 | Clamped stage is visually distinct |
| **T-054** | Checkout: tender slider bounded by `maxCredits`, submission with a single idempotency key, **saga timeline** with compensation rendering. | FR-B-08, FR-L-06 | Forced failure animates the unwind and states nothing was charged |
| **T-055** | Wallet: balance and statement with reversal links. | FR-L-12 | Reversal links to its original |
| **T-056** | Operator view: saga list and step timeline, review-needed first. | FR-B-08 | US-12 acceptance criteria met |
| **T-057** | Frontend tests: store transitions, mappers, component rendering with fake ports. | NFR-09 | No HTTP mock anywhere in component tests |

**Milestone:** the whole core journey is usable in a browser, including watching a booking fail and unwind.

---

## 7. Phase 5 — F4 Concierge and MCP *(core)*

| Task | Description | Satisfies | Verify |
|---|---|---|---|
| **T-060** | Criteria parser, candidate pipeline, affordability filter, weighted ranking. | FR-C-01 … FR-C-04 | Deterministic across repeated runs |
| **T-061** | `RecommendationAudit` with exclusions and reasons. | FR-C-05 | Every excluded candidate has a reason |
| **T-062** | `IOfferNarrator`, `NullOfferNarrator`, fact validator, template fallback. | FR-C-06, FR-C-07 | Narration inventing a price is rejected and falls back |
| **T-063** | `POST /concierge/recommend`. | FR-C-01 | Integration test |
| **T-064** | MCP server with three tools over the same use cases. | FR-C-08 | Tool and REST results agree for identical input |
| **T-065** | Concierge UI with the collapsible audit disclosure. | FR-C-05 | Audit visible and readable |
| **T-066** | Grounding and prompt-injection tests. | FR-C-09, G9 | Adversarial prompt leaks no foreign data |

**Milestone:** recommendations are always real, always affordable, always auditable — and an agent gets the same guarantees.

---

## 8. Phase 6 — F5 Opportunity engine *(stretch)*

**Start only if Phases 0–5 are complete and green.** Everything below is designed to be droppable without leaving a visible hole.

| Task | Description | Satisfies |
|---|---|---|
| **T-070** | Domain: `TravelWindow`, `OpportunitySignal`, `Nudge`, `SuppressionReason`, persistence. | FR-O-01, FR-O-05 |
| **T-071** | Window detection and deterministic signal scoring, priced via the normal engine. | FR-O-01, FR-O-02, FR-O-04 |
| **T-072** | Fatigue rules in order, with suppressions persisted. | FR-O-06 |
| **T-073** | `PriceWatch` baselines and the batched scan worker. | FR-O-03, FR-O-11 |
| **T-074** | `/inbox`, action, and dismiss — actioning re-quotes. | FR-O-07, FR-O-09, FR-O-10 |
| **T-075** | Inbox UI with the "why am I seeing this?" signal breakdown. | FR-O-05 |
| **T-076** | Tests: detection, scoring, each suppression reason, expiry. | G15, G16 |

**Milestone:** a nudge appears with its reasoning, and a second one is suppressed with a recorded reason.

---

## 9. Phase 7 — Polish *(core)*

| Task | Description |
|---|---|
| **T-080** | README: prerequisites from a bare machine, `scripts/run-all.ps1`, three-terminal alternative, troubleshooting. |
| **T-081** | Demo script — the numbered walkthrough from [problem statement §7](01-problem-statement.md), verified end to end on a clean clone. |
| **T-082** | Documentation and code consistency pass; resolve every open question in [design §12](04-detailed-design.md#12-open-questions). |
| **T-083** | Full suite run, warning sweep, fresh-clone verification. |

---

## 10. Day plan

| Day | Focus | Ends with |
|---|---|---|
| **1** | Phase 0 and most of Phase 1 | Two partners, two prices, one explanation |
| **2** | Finish Phase 1, all of Phase 2 | Ledger correct under property-based tests |
| **3** | Phase 3 | Bookings break and unwind cleanly |
| **4** | Phase 4, start Phase 5 | Full journey usable in a browser |
| **5** | Finish Phase 5, then Phase 7 | Demo-ready; **F5 only if genuinely ahead** |

Day 3 carries the most risk and the least slack. If Phase 3 slips into day 4, F5 is cut without further deliberation — that is what the cut line is for.

---

## 11. Commit convention

```
[T-011] Add pricing pipeline stages with margin floor

Implements the eight ordered stages from design §2.2. The floor
executes after campaigns so a stacked discount cannot push the
price below cost.

Satisfies: FR-P-01, FR-P-05, FR-P-10
```

Task id, what changed, why the non-obvious choice was made, requirements satisfied. A reviewer reading `git log` should be able to follow the build without opening a file.

---

## 12. Cut line

Dropped in this order, highest number first:

| Order | Dropped | Cost of dropping |
|---|---|---|
| 1 | **F5 entirely** (T-070 … T-076) | One of five features. Fully designed and documented, so the omission reads as scope control rather than failure. |
| 2 | Playwright end-to-end tests | Integration tests already cover the paths |
| 3 | Operator retry actions (FR-B-13) | Marked COULD; the read-only operator view is what US-12 needs |
| 4 | Pricing simulation view (FR-P-12) | Marked COULD |
| 5 | Reconciliation report (FR-L-11) | Marked SHOULD; the ledger is still correct without it |

**Never cut, regardless of schedule:** architecture tests, the raw-JSON rate-leak assertion, ledger property-based tests, the saga crash-recovery test, and the prompt-injection test. Each of these is the *only* evidence for a claim this project makes, and a claim without evidence is worse than an absent feature.

If F5 is cut, [README](../README.md) and this document mark it *designed, not implemented*, with a link to its design. Documented-and-deferred is a defensible engineering position; half-built and unmentioned is not.

---

## 13. Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Phase 3 overruns | High | F5 is pre-cut; the simulator is built first so integration risk surfaces on day 3, not day 5 |
| SQLite concurrency under saga tests | Medium | WAL mode; serialize resilience tests; retry on transient lock |
| Angular 21 API differences from expectation | Medium | Scaffold on day 1 rather than day 4, so surprises are cheap |
| Documentation drifts from code | Medium | Definition of done requires same-commit updates (NFR-11) |
| Polish gets squeezed | Medium | Phase 7 is core, not optional; F5 yields to it |
| Fresh-clone startup fails on another machine | Low | T-083 verifies from a clean clone before submission |

---

**Back to:** [README](../README.md) · [Documentation index](.)
