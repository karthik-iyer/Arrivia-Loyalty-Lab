# 06 — Future Improvements

| | |
|---|---|
| **Document** | Production roadmap and known gaps |
| **Status** | Living |
| **Prerequisite reading** | [01 — Problem statement §5](01-problem-statement.md), [ADRs](adr/) |

Everything here is a **deliberate omission**, not an oversight. Each item states what is missing, why it was acceptable to omit it, and what implementing it would actually involve. Where a decision would be reversed rather than extended, the relevant [ADR](adr/) is linked.

Knowing what you did not build — and being able to say what it would cost — is a more useful signal than a longer feature list.

---

## 1. The honest gaps

Read this section first. These are the things that would be irresponsible to leave unstated.

| Gap | Current state | Why acceptable here |
|---|---|---|
| **No authentication** | Identity comes from a request header ([ADR-0005](adr/0005-header-based-demo-identity.md)) | The interesting problem is authorization, which *is* implemented. Anyone can claim any identity. |
| **No transport security** | HTTP in local development | Nothing sensitive leaves the machine |
| **No rate limiting** | Unbounded request rates | Single-user demo |
| **No secret management** | Optional model key from user secrets | No production secrets exist |
| **Simulated supplier and payment** | Deterministic simulators | Real connectivity adds vendors, not insight — though payment is deliberately out of process so the distributed problem is real ([ADR-0006](adr/0006-payment-out-of-process-and-saga.md)) |
| **Single currency per partner** | Both seeded partners use USD | Multi-currency is a large, well-understood problem that would crowd out the ones being demonstrated |
| **No PII handling** | Member names are seed data | No real personal data exists |

---

## 2. Security and identity

**OpenID Connect with a real provider.** `TenantContext` is populated in exactly one middleware, so the change is narrow: map claims instead of headers. Partner becomes a claim rather than a header, which also closes the tenant-spoofing hole. Roles move from a seeded enum to provider groups.

**Fine-grained authorization policies.** Roles today gate net-rate visibility and the operator view. Production needs resource-based checks — an account manager for Partner A must not administer Partner B — expressed as ASP.NET authorization policies rather than conditionals inside handlers.

**Secrets and configuration.** Azure Key Vault or equivalent, with connection strings and model keys resolved at startup and no secret in `appsettings`.

**Rate limiting and abuse protection.** Per-partner and per-member quotas, with the concierge endpoint separately limited because it is the most expensive path and the most attractive to abuse.

**Audit logging as a first-class store.** Pricing, ledger, saga, and nudge decisions are already explainable from persisted data, which is most of the work. What is missing is a tamper-evident, retained, queryable audit store with a defined retention policy.

**Transport and data protection.** TLS everywhere, encryption at rest, and PII classification with a deletion path for data-subject requests.

---

## 3. Correctness and data

**Multi-currency.** Requires a rate source, a policy on when rates are captured — quote time, booking time, or settlement time, and they differ — and a decision on whether credits are currency-denominated or a universal unit. `Money` already carries currency and refuses cross-currency arithmetic, so the type system is ready; the business rules are not written.

**Partial cancellation and refund policies.** Today cancellation is all or nothing. Real programs have non-refundable components, tiered penalties, and date-dependent rules. The ledger handles this without structural change — a partial reversal is still a balanced transaction — but the policy engine deciding *how much* to reverse does not exist.

**Promotion stacking.** One campaign per quote today, chosen by precedence. Real programs stack with rules about combinability, order, and caps. The pipeline extends naturally: `CampaignDiscountStage` becomes a sub-pipeline with its own precedence.

**Accrual on stay rather than on booking.** Credits are earned at booking for demo immediacy. Real programs accrue on completed stay, which means pending accruals, a stay-completion signal from the supplier, and forfeiture on cancellation.

**Balance snapshots.** FR-L-13 leaves room for periodic snapshots as an accelerator with the ledger still authoritative ([ADR-0011](adr/0011-derived-balances-not-stored.md)). Worth doing when entry counts per member reach the thousands, and not before.

**Breakage forecasting.** `PartnerBreakage` records expired credits. Finance teams want to *forecast* breakage, which is a modelling exercise over historical redemption behaviour.

---

## 4. Resilience and operations

**Replace the hand-rolled orchestrator with a library.** [ADR-0008](adr/0008-hand-rolled-saga-orchestration.md) chose hand-rolled deliberately, to make the mechanics visible. That reasoning applies to a POC and not to a system taking real bookings. MassTransit or Temporal would supply retry policy, backoff, state persistence, and recovery scanning that are currently maintained here — and would be better tested than any bespoke version.

**A real broker behind `IOutbox`.** The outbox stays; the dispatcher targets Kafka or Azure Service Bus instead of an in-process handler ([ADR-0007](adr/0007-transactional-outbox-not-broker.md)). Dual-write protection is already in place, which is the part that is hard to retrofit.

**Dead-letter handling with an operator workflow.** Poisoned messages are stored and visible. Production needs replay, bulk actions, and alerting on queue depth.

**Distributed tracing.** Correlation ids propagate through the outbox today. OpenTelemetry spans across API, payment simulator, and workers would make a saga a single trace rather than a set of correlated logs.

**Health checks, metrics, and alerting.** Liveness and readiness endpoints; metrics on saga terminal-state distribution, compensation rate, outbox lag, and quote expiry rate. `RequiresManualReview` should page someone, since by definition automation has given up.

**Chaos testing in CI.** `Resilience.Tests` injects specific faults. Randomized fault injection across a booking corpus, run nightly, would find the combinations nobody thought to write a test for.

**Backup, restore, and disaster recovery.** Not meaningful for a SQLite file; essential for a ledger. Point-in-time restore with a tested restore procedure, because an untested backup is a hypothesis.

---

## 5. Scale and performance

**PostgreSQL with read replicas.** EF Core makes the provider swap small; the work is in indexing strategy and connection management ([ADR-0002](adr/0002-single-solution-single-database.md)).

**Rate caching.** The simulated supplier answers instantly, so no cache is justified. Real suppliers are slow, rate-limited, and often billed per call. A cache needs a TTL policy per supplier, negative caching, and a stampede guard — and it interacts directly with rate drift, since a cached rate is a stale rate by definition.

**Pricing result caching.** Prices are deterministic given rules, offer, member tier, and `asOf`, which makes them cacheable on exactly that key. Invalidation is rule activation, which is already an explicit, dated event.

**Horizontal scale for workers.** The outbox dispatcher and saga recovery are single-instance today. Multiple instances need leasing or partitioning so two workers do not process one saga.

**Opportunity scan at scale.** The current scan iterates members. At millions of members this becomes a streaming or batch pipeline, with price-watch checks driven by supplier change feeds rather than polling.

**Read models for reporting.** Liability reporting aggregates the ledger. Beyond a certain volume this wants a separate read model updated from the outbox, keeping the transactional path fast.

---

## 6. AI maturity

**Semantic criteria parsing.** Keyword matching is deterministic and limited ([ADR-0009](adr/0009-deterministic-core-llm-narration-only.md)). An embedding-based parser would handle "somewhere warm in February, not too touristy" — the key constraint being that it produces *structured criteria* which deterministic code then evaluates. The model chooses filters; it still never chooses prices.

**RAG over partner content.** Destination guides, property descriptions, and policies retrieved to enrich narration, with citations. Grounding rules unchanged: retrieved content may inform prose, never facts about price or availability.

**Learned ranking.** Weights are hand-set and explainable. A model trained on conversion would rank better and explain worse. The honest path is a hybrid — learned scoring with the deterministic eligibility and affordability filters still applied first, so the model can reorder what is bookable but never introduce what is not.

**Expanded MCP surface.** Booking initiation, saga status, and wallet operations as tools, with agent-appropriate confirmation semantics for anything that moves money.

**Evaluation harness.** A golden set of requests with expected inclusions and exclusions, run in CI. Today's determinism makes this straightforward; the moment a model enters the selection path, it becomes mandatory.

---

## 7. Frontend

**Server-side rendering.** Catalog pages benefit from SSR for first-paint and for partner-branded sharing.

**Real-time saga updates.** Polling backs off and stops on terminal status, which is adequate. SignalR pushed from outbox handlers would be cleaner and would remove the polling path entirely.

**Offline and optimistic UI.** Wallet history and catalog results cached for read-only offline use.

**Internationalization.** The application is English-only and formats currency for one locale. White-label almost always implies multiple markets.

**Design system extraction.** Shared components are project-local. A published token set and component library would let partners theme beyond colour.

**Visual regression testing.** Partner theming makes CSS changes risky in a way unit tests do not catch.

---

## 8. Delivery

**CI/CD.** Build, test, architecture tests, lint, and container publish on every commit. Architecture tests gating merges is the single highest-value pipeline step, since it prevents the layering from eroding one convenient shortcut at a time.

**Containerization and infrastructure as code.** Dockerfiles per process, Bicep or Terraform for Azure, environment promotion with configuration separated from artifacts.

**Blue-green deployment with migration safety.** Ledger migrations are the sensitive case: append-only data and long-lived saga instances mean schema changes must be forward-compatible for at least one release.

**Feature flags.** Fault injection is already flag-gated. A real flag system would allow per-partner rollout of pricing rule types and opportunity-engine changes.

**Load and soak testing.** Particularly around saga throughput and outbox lag, which are the components most likely to degrade non-linearly.

---

## 9. Prioritized roadmap

### If I had one more day

1. Complete F5 if it was cut, or expand `Resilience.Tests` with randomized fault combinations.
2. Add the operator retry and force-compensate actions (FR-B-13).
3. Add the pricing simulation view (FR-P-12) — it is the feature account managers would actually ask for.
4. Playwright end-to-end tests for the two-partner comparison and a forced booking failure.
5. OpenTelemetry tracing so a saga renders as one trace.

### If I had one more week

1. OpenID Connect replacing header identity, with tenant as a claim.
2. PostgreSQL with a tested migration path.
3. Real broker behind `IOutbox`, with dead-letter replay.
4. Partial cancellation with a refund policy engine.
5. CI pipeline with architecture tests gating merges.
6. Health checks, metrics, and alerting on `RequiresManualReview`.

### If I had one more month

1. Replace the bespoke orchestrator with MassTransit or Temporal.
2. Multi-currency with a defined rate-capture policy.
3. Semantic criteria parsing with an evaluation harness.
4. Rate caching with stampede protection, integrated with drift handling.
5. Read models for reporting, fed from the outbox.
6. Real supplier integration for one supplier, proving the port abstraction under a real API.

### Before production

Non-negotiable regardless of schedule: authentication and authorization, TLS, secret management, PII handling with a deletion path, backup and tested restore, audit retention, rate limiting, and an on-call runbook covering `RequiresManualReview`, outbox lag, and ledger reconciliation failures.

---

**Next:** [07 — Task breakdown](07-task-breakdown.md)
