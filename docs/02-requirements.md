# 02 — Requirements & User Stories

| | |
|---|---|
| **Document** | Requirements specification |
| **Status** | Approved for design |
| **Prerequisite reading** | [01 — Problem statement](01-problem-statement.md) |

Requirement identifiers are stable and are referenced by the [detailed design](04-detailed-design.md) and every implementation task in the [task breakdown](07-task-breakdown.md). Nothing gets built that is not traceable to an identifier here.

**Prefixes:** `FR-X` platform · `FR-P` pricing · `FR-L` ledger · `FR-B` booking saga · `FR-C` concierge · `FR-O` opportunity engine · `NFR` non-functional.

**Priority:** `MUST` demo fails without it · `SHOULD` materially strengthens the demo · `COULD` include only if time remains.

All five features ship in **one solution** as vertical slices sharing a domain, a database, and a host — see [High-level design](03-high-level-design.md#3-solution-structure). Implementation order and the scope cut line are in the [task breakdown](07-task-breakdown.md).

---

## 1. Cross-cutting platform requirements

| ID | Priority | Requirement |
|---|---|---|
| FR-X-01 | MUST | Every inbound request resolves to exactly one partner (tenant) context before any business logic executes. A request that cannot be attributed to a partner is rejected. |
| FR-X-02 | MUST | All partner-owned data access is scoped by the resolved partner. Cross-partner reads must be impossible by construction, not by convention. |
| FR-X-03 | MUST | Member context — identity, partner, tier, credit balance — is resolved once per request and passed explicitly, never re-fetched ad hoc deeper in the stack. |
| FR-X-04 | MUST | Partner presentation (name, logo, colour palette) is served from configuration so that a new brand requires no code change. |
| FR-X-05 | MUST | An unauthenticated caller may see that inventory exists, but never a net rate and never a member price. |
| FR-X-06 | MUST | Monetary amounts use decimal arithmetic with an explicit currency. Floating-point types are banned from the domain. Credits are whole integers. |
| FR-X-07 | MUST | Business thresholds — margin floors, burn caps, drift tolerance, nudge weights, fatigue caps — are partner configuration. Changing one must not require a code change. |
| FR-X-08 | SHOULD | Every response carries a correlation identifier that appears in all logs emitted while handling that request, including asynchronous work it triggers. |
| FR-X-09 | SHOULD | Seeded demonstration data is deterministic, so that a documented demo script produces identical results on any machine. |
| FR-X-10 | COULD | A demo control panel allows switching partner and member without re-authenticating. |

---

## 2. Feature 1 — Pricing & margin engine

### 2.1 Functional requirements

| ID | Priority | Requirement |
|---|---|---|
| FR-P-01 | MUST | The engine converts a supplier net rate into a member-facing price by executing an ordered, deterministic sequence of stages. The same inputs always produce the same output. |
| FR-P-02 | MUST | Supported rule types: supplier/offer **eligibility exclusion**, **base markup**, **tier adjustment**, **campaign discount**, **credits burn cap**, and **minimum margin floor**. |
| FR-P-03 | MUST | Rules are effective-dated with an inclusive start and exclusive end, so a historical price can be reproduced by replaying against the rules in force at that time. |
| FR-P-04 | MUST | Rule precedence is deterministic, documented, and total — two rules can never tie ambiguously. |
| FR-P-05 | MUST | The minimum margin floor is a hard guardrail. No combination of markups, tier adjustments, or campaigns may produce a member price below net cost plus the configured floor. |
| FR-P-06 | MUST | A successful pricing run yields an immutable **quote** carrying the priced amount, the tender split limits, and an expiry timestamp. |
| FR-P-07 | MUST | A quote can be explained. The explanation lists every stage in execution order with its input, its effect, and the running subtotal. |
| FR-P-08 | MUST | Explanations are role-aware. A member sees the price composition; net rate and absolute margin are visible only to internal roles. |
| FR-P-09 | MUST | Checkout re-validates the quote. An expired quote is rejected with a machine-readable reason and a re-quote path, never silently repriced. |
| FR-P-10 | MUST | Rounding is applied once, at a defined stage, using a documented rounding mode. Intermediate stages retain full precision. |
| FR-P-11 | MUST | If the supplier rate changed since the quote was issued, the configured drift policy decides between absorbing the difference within margin tolerance and requiring a re-quote. |
| FR-P-12 | COULD | An account-manager view can simulate a hypothetical rule change against real inventory without persisting it. |

### 2.2 Worked example

Illustrative only; authoritative stage semantics live in the [detailed design](04-detailed-design.md).

```
Supplier net rate                            100.00
  1. Eligibility          supplier permitted     ok
  2. Base markup          Partner A +12%      112.00
  3. Tier adjustment      Gold -3%            108.64
  4. Campaign             MARCH-BEACH -5%     103.21
  5. Margin floor         floor = net +5%     103.21  (105.00 required -> raised)
                                              105.00
  6. Rounding             to nearest cent     105.00
  7. Burn cap             Partner A max 40%    42.00 payable in credits
```

The floor firing in step 5 is the interesting case: a campaign that would otherwise have sold below the commercial minimum was clamped, and the explanation records that it was clamped.

---

## 3. Feature 2 — Savings Credits ledger

### 3.1 Functional requirements

| ID | Priority | Requirement |
|---|---|---|
| FR-L-01 | MUST | The ledger is append-only. Entries are never updated or deleted; corrections are made by posting compensating entries. |
| FR-L-02 | MUST | Every transaction consists of balanced double-entry legs summing to zero. An unbalanced transaction cannot be persisted. |
| FR-L-03 | MUST | Supported transaction types: **earn**, **burn**, **expire**, **reversal**, and **manual adjustment**. |
| FR-L-04 | MUST | A member's balance is derived from ledger entries rather than stored as an independently mutable field. |
| FR-L-05 | MUST | Every mutating operation requires a caller-supplied idempotency key. Replaying a key returns the original result and creates no additional entries. |
| FR-L-06 | MUST | A burn is rejected if it exceeds the available balance or the partner's burn cap for that booking. |
| FR-L-07 | MUST | A mixed cash-and-credits payment settles atomically from the member's perspective: either the booking is confirmed with both tenders applied, or neither is retained. |
| FR-L-08 | MUST | A reversal references the transaction it reverses and restores the exact original amounts. Reversals are never recomputed from current rules. |
| FR-L-09 | MUST | Credits carry an expiry date, and expiry is applied as an explicit ledger transaction rather than inferred at read time. |
| FR-L-10 | MUST | An outstanding-liability report is available per partner, as of any date, derived from the ledger. |
| FR-L-11 | SHOULD | A reconciliation routine proves that ledger movements agree with booking records, and reports any discrepancy rather than silently correcting it. |
| FR-L-12 | SHOULD | A member-facing statement shows each transaction with its reason and the resulting running balance. |
| FR-L-13 | COULD | Balance snapshots at intervals accelerate reads on long histories without becoming the source of truth. |

### 3.2 Invariants

These hold at all times and are enforced by tests:

1. The sum of all entry amounts within a transaction is exactly zero.
2. A member's derived balance is never negative.
3. Every reversal references exactly one prior transaction, and no transaction is reversed twice.
4. Total credits issued minus total burned minus total expired equals total outstanding liability.
5. Replaying any accepted request with its original idempotency key changes nothing.

---

## 4. Feature 3 — Resilient booking saga

Checkout spans a supplier reservation system, a payment service, the ledger, and the local booking record. These cannot share a database transaction, so consistency is achieved through explicit orchestration and compensation.

### 4.1 Functional requirements

| ID | Priority | Requirement |
|---|---|---|
| FR-B-01 | MUST | Booking executes as an explicit saga of named steps, each with a defined compensating action. |
| FR-B-02 | MUST | Saga state is persisted before each external call, so a process that dies mid-flight resumes rather than restarts. |
| FR-B-03 | MUST | Every external call carries an idempotency key, so a retry produces exactly one effect at the far end. |
| FR-B-04 | MUST | A timed-out external call is recorded as **unknown**, not failed. The saga resolves the ambiguity by querying the external system before choosing to proceed or compensate. |
| FR-B-05 | MUST | Compensations execute in reverse order of step completion. A failed compensation is retried with backoff and escalated after a bounded number of attempts. |
| FR-B-06 | MUST | Local state changes and outbound events are committed in a single transaction using a transactional outbox, so no event is lost if the process dies after committing. |
| FR-B-07 | MUST | Outbox dispatch is at-least-once with retry, and messages that exhaust retries move to a poison queue rather than blocking the pipeline. |
| FR-B-08 | MUST | A saga instance is inspectable: every step with status, attempt count, timings, error, and compensation outcome. |
| FR-B-09 | MUST | Faults can be injected deliberately — supplier timeout, supplier rejection, payment decline, payment timeout, and a simulated crash between any two steps. |
| FR-B-10 | MUST | Terminal states are exhaustive and explicit: `Confirmed`, `Compensated`, or `RequiresManualReview`. A saga is never left in an indeterminate state. |
| FR-B-11 | SHOULD | A recovery worker detects sagas stalled beyond a timeout and drives them to a terminal state. |
| FR-B-12 | SHOULD | Concurrent attempts against the same booking are serialized, so a double submission cannot interleave. |
| FR-B-13 | COULD | An operator can manually retry a step or force compensation from the operator view. |

### 4.2 Saga steps and compensations

| # | Step | External? | Compensation | Notes |
|---|---|---|---|---|
| 1 | Validate quote and apply drift policy | no | none (read-only) | May terminate early with `QUOTE_EXPIRED` or `RATE_CHANGED` |
| 2 | Reserve supplier inventory | yes | Release reservation | Cheapest to undo, so it goes first |
| 3 | Authorize payment | yes | Void authorization | Authorization only, not capture |
| 4 | Burn credits | no (ledger) | Post reversal transaction | Uses FR-L-08 semantics |
| 5 | Capture payment | yes | Refund payment | Only after both tenders are secured |
| 6 | Confirm booking and accrue earn | no | Mark cancelled and reverse earn | Terminal on success |

Ordering rationale: the reservation is placed before money moves because releasing a hold is cheap and reliable, whereas refunding a captured payment is slow and visible to the member. Authorization is separated from capture so that a credits failure at step 4 costs a void rather than a refund.

### 4.3 Failure semantics

| Situation | Required behaviour |
|---|---|
| Supplier declines at step 2 | Terminate as `Compensated`. Nothing to undo. |
| Payment declined at step 3 | Compensate step 2. Terminate `Compensated`. |
| Insufficient credits at step 4 | Compensate steps 3 and 2. Terminate `Compensated`. |
| Capture fails at step 5 | Compensate steps 4, 3, 2. Terminate `Compensated`. |
| Supplier times out at step 2 | Mark unknown, query supplier for the reservation by idempotency key, then proceed or compensate on the answer. |
| Process crashes between any two steps | Recovery worker resumes from persisted state; no duplicate external effect because of FR-B-03. |
| A compensation fails repeatedly | Terminate `RequiresManualReview` and surface in the operator view. |

---

## 5. Feature 4 — Grounded concierge & MCP server

### 5.1 Functional requirements

| ID | Priority | Requirement |
|---|---|---|
| FR-C-01 | MUST | The concierge accepts a natural-language request alongside optional structured filters such as dates, destination, and budget. |
| FR-C-02 | MUST | The candidate set is restricted to inventory the resolved partner is permitted to sell and the member's tier is entitled to. |
| FR-C-03 | MUST | Candidates are filtered for affordability using the member's live credit balance and the applicable burn cap. |
| FR-C-04 | MUST | Every returned recommendation references a real offer identifier and a real, freshly generated quote. The concierge may not state a price it did not obtain from the pricing engine. |
| FR-C-05 | MUST | Each response carries an audit block: how many candidates were considered, which were excluded, the reason for each exclusion, and the ranking signals applied. |
| FR-C-06 | MUST | The recommendation core is deterministic and rules-based. A language model may only rephrase results into prose. |
| FR-C-07 | MUST | If the language model is unavailable or unconfigured, the concierge degrades to structured output and remains fully functional. |
| FR-C-08 | MUST | The same capability is exposed as a **Model Context Protocol** server so that an external AI agent can call it under identical constraints. |
| FR-C-09 | MUST | Tenant isolation holds under adversarial input. No prompt may cause the concierge to reveal or reason over another partner's configuration, inventory, or members. |
| FR-C-10 | SHOULD | Recommendations are explainable in member-friendly language: why this offer, for this member, now. |
| FR-C-11 | COULD | Conversation context persists across turns within a session. |

### 5.2 The grounding boundary

| The model may | The model may not |
|---|---|
| Rephrase a supplied offer list into prose | Invent an offer, a hotel, or a destination |
| Explain a supplied price composition | State, estimate, or adjust any price |
| Ask a clarifying question | Decide eligibility or affordability |
| Suggest a structured filter to apply | Access data outside the resolved tenant |

Every fact in a concierge response originates in the domain. The model contributes wording only.

---

## 6. Feature 5 — Opportunity engine *(stretch)*

Turns the platform from reactive to proactive: notice a plausible trip before the member searches for one.

### 6.1 Functional requirements

| ID | Priority | Requirement |
|---|---|---|
| FR-O-01 | MUST | Detect **travel windows** from a member's availability feed — gaps meeting a configured minimum duration and minimum lead time. |
| FR-O-02 | MUST | Match each window against partner-eligible inventory, priced through the normal pricing engine rather than a shortcut. |
| FR-O-03 | MUST | Watch candidate offers for price movement and detect a drop beyond a configured threshold. |
| FR-O-04 | MUST | Score opportunities deterministically from named signals: window fit, destination and tag affinity from booking history, credit coverage, and price-drop magnitude. |
| FR-O-05 | MUST | Every generated nudge persists its trigger signals and score, so it can be explained after the fact. |
| FR-O-06 | MUST | Fatigue rules cap nudges per member per period and impose a cooldown after a dismissal. A suppressed nudge records that it was suppressed and why. |
| FR-O-07 | MUST | Nudges expire. An expired nudge is neither displayed nor actionable. |
| FR-O-08 | MUST | Thresholds, signal weights, and fatigue caps are partner configuration (FR-X-07). |
| FR-O-09 | MUST | Actioning a nudge generates a fresh quote through the normal pricing path. A nudge never carries a stale price into checkout. |
| FR-O-10 | SHOULD | A member can dismiss a nudge, and dismissal feeds the suppression rules. |
| FR-O-11 | SHOULD | Price watching is batched and throttled so that supplier call volume stays bounded as membership grows. |
| FR-O-12 | COULD | An attribution report relates generated nudges to resulting bookings. |

### 6.2 Suppression is a feature, not an omission

The engine must be able to explain why it *didn't* send something. Suppression reasons are first-class and recorded: `FatigueCapReached`, `CooldownActive`, `ScoreBelowThreshold`, `NoEligibleInventory`, `WindowTooSoon`, `DuplicateOfRecentNudge`. Demonstrating a deliberate silence is a stronger signal of engineering judgement than demonstrating a notification.

---

## 7. Non-functional requirements

| ID | Category | Requirement |
|---|---|---|
| NFR-01 | Architecture | Layer dependencies are enforced by automated architecture tests that fail the build on violation. Domain depends on nothing; Application depends only on Domain; Infrastructure and Api depend inward only. |
| NFR-02 | Testability | Domain and Application layers are testable with no database, no HTTP, and no clock dependency. Time is injected. |
| NFR-03 | Correctness | Pricing and ledger logic carry unit tests covering documented edge cases, plus property-based tests for the ledger invariants in §3.2. |
| NFR-04 | Security | Private rates are never serialized to an unauthorized caller. This is asserted by integration tests against raw response payloads, not left to code review. |
| NFR-05 | Auditability | Every price, every recommendation, every saga outcome, and every nudge can be explained after the fact from persisted data alone. |
| NFR-06 | Observability | Structured logs include correlation identifier, partner, member, and — where applicable — saga instance on every business operation. |
| NFR-07 | Performance | On a developer machine with seeded data, search-and-price returns within 500 ms at the 95th percentile; a concierge request without model narration within 800 ms. |
| NFR-08 | Portability | The solution runs from a clean clone with a documented startup. No cloud account, no API key, no external service, no broker installation. |
| NFR-09 | Frontend architecture | Components never call HTTP directly. Feature state is exposed through facades over use cases, which depend on ports implemented by a data layer. |
| NFR-10 | Accessibility | Interactive elements are keyboard reachable and labelled; contrast meets WCAG AA. |
| NFR-11 | Documentation | Design documents stay current with the code. A change to pricing precedence, ledger semantics, or saga steps updates the design document in the same commit. |
| NFR-12 | Reproducibility | Seeded data and a fixed demo clock make the documented demo script produce identical output on any machine. |
| NFR-13 | Resilience | Every saga step is idempotent and safe to retry. Recovery after a killed process is demonstrated by an automated test, not asserted in prose. |
| NFR-14 | Safety | Fault injection is disabled by default and can only be enabled by explicit configuration, never in a production profile. |

---

## 8. User stories

Written from the personas in [§6 of the problem statement](01-problem-statement.md#6-personas). Acceptance criteria are the basis for the tests.

### US-01 — Member sees her own price

> As **Maya**, a member of a bank rewards program, I want to see prices that already reflect my membership and tier, so that I know what I would actually pay.

- Given Maya is signed in to Partner A as a Gold member, when she searches, then every result shows a member price computed by the pricing engine.
- Given the same offer is viewed by a Partner B member, then the displayed price differs according to configuration alone.
- Given a signed-out visitor, then no member price and no net rate appear anywhere in the response payload.

*Satisfies:* FR-P-01, FR-P-02, FR-X-02, FR-X-05

### US-02 — Account manager explains a price

> As **Devin**, a partner account manager, I want to see exactly how a price was produced, so that I can answer a partner's question without escalating to engineering.

- Given any quote, when Devin opens the explanation, then each stage appears in execution order with its input, effect, and running subtotal.
- Given a rule was clamped by the margin floor, then the explanation states that it was clamped and by how much.
- Given Devin holds an internal role, then net rate and margin are visible; given a member role, they are absent from the payload entirely.

*Satisfies:* FR-P-07, FR-P-08, FR-P-05, NFR-05

### US-03 — Member pays with cash and credits

> As **Maya**, I want to pay for part of my booking with credits, so that I get tangible value from my rewards.

- Given a booking of 105.00 and a partner burn cap of 40%, then at most 42.00 may be paid with credits.
- Given Maya has insufficient credits, then the credit portion is capped at her available balance and the cash portion adjusts accordingly.
- Given the cash authorization fails, then no credits are retained and no booking is created.
- Given Maya submits the same booking twice with one idempotency key, then exactly one booking and one ledger transaction exist.

*Satisfies:* FR-L-06, FR-L-07, FR-L-05, FR-P-06, FR-B-03

### US-04 — Cancellation restores the exact prior state

> As **Maya**, I want cancelling to return exactly the credits I spent, so that I am never quietly shortchanged.

- Given a completed mixed-tender booking, when it is cancelled, then a reversal transaction referencing the original restores the exact credit amount.
- Given rules changed between booking and cancellation, then the reversal still uses the original amounts.
- Given a cancellation is submitted twice, then only one reversal exists.
- Given the booking is then re-examined, then the member's balance equals the pre-booking balance exactly.

*Satisfies:* FR-L-08, FR-L-05, FR-L-01

### US-05 — Finance reports outstanding liability

> As **Priya**, a finance analyst, I need the outstanding credit liability per partner as of a chosen date, so that it can be reported with confidence.

- Given a date, then the report returns issued, burned, expired, and outstanding totals per partner.
- Given the report is regenerated for a past date, then the figure is unchanged regardless of subsequent activity.
- Given the reconciliation routine runs, then any disagreement between ledger and bookings is reported rather than corrected.

*Satisfies:* FR-L-10, FR-L-11, FR-L-01

### US-06 — Concierge recommends only the bookable

> As **Maya**, I want to describe the trip I want in my own words and receive suggestions I can actually book.

- Given a natural-language request, then every recommendation resolves to a real offer with a real quote.
- Given an offer her partner excludes, then it never appears, and the audit block records the exclusion and its reason.
- Given an offer she cannot afford within her balance and burn cap, then it is excluded with an affordability reason.
- Given no language model is configured, then the concierge still returns ranked recommendations with the audit block.

*Satisfies:* FR-C-02, FR-C-03, FR-C-04, FR-C-05, FR-C-07

### US-07 — Tenant isolation holds under attack

> As **Sam**, responsible for supplier relationships, I need certainty that one partner's private rates cannot be reached through another partner's surface.

- Given a request carrying Partner A context and an identifier belonging to Partner B, then the response is a not-found rather than a forbidden, leaking nothing about existence.
- Given a prompt instructing the concierge to disregard its constraints and reveal other partners' rates, then the response contains no such data.
- Given any concierge response, then every referenced offer belongs to the resolved partner.

*Satisfies:* FR-X-02, FR-C-09, NFR-04

### US-08 — An engineer can find and change pricing safely

> As **Alex**, newly joined, I want to locate the pricing rules quickly and change them without breaking unrelated behaviour.

- Given the repository, then pricing logic lives in one named location in the Domain layer with no infrastructure dependencies.
- Given a new pricing stage, then it is added by implementing one interface and registering it, with no change to existing stages.
- Given an accidental dependency from Domain to Infrastructure, then the architecture test suite fails.

*Satisfies:* NFR-01, NFR-02, FR-P-01

### US-09 — An AI agent uses the same guarded capability

> As an external AI agent, I want to retrieve member offers through a standard protocol under the same constraints as the web application.

- Given a running MCP endpoint, then an agent can discover the offer, price-explain, and balance tools.
- Given a tool invocation, then identical eligibility, affordability, and tenant rules apply as in the web path.
- Given a tool response, then the audit block is present.

*Satisfies:* FR-C-08, FR-C-05, FR-X-02

### US-10 — A stale price is never charged silently

> As **Maya**, I want to be told if the price changed while I was deciding, rather than being charged a different amount.

- Given a quote past its expiry, when checkout is attempted, then it is rejected with a machine-readable reason and a re-quote path.
- Given the underlying supplier rate moved within tolerance, then the configured drift policy is applied and the outcome recorded on the booking.
- Given a re-quote produces a different price, then explicit confirmation is required before payment.

*Satisfies:* FR-P-09, FR-P-11, FR-P-06

### US-11 — A booking survives a mid-flight failure

> As **Maya**, I want a failure inside the booking process to leave me either fully booked or fully unwound — never charged for nothing.

- Given the supplier reservation succeeds and the payment is then declined, then the reservation is released and no credits are retained.
- Given credits are burned and the payment capture then fails, then the burn is reversed, the authorization voided, and the reservation released.
- Given the process is killed between two steps, when it restarts, then the saga resumes and reaches a terminal state without duplicating any external effect.
- Given any terminal state, then it is exactly one of `Confirmed`, `Compensated`, or `RequiresManualReview`.

*Satisfies:* FR-B-01, FR-B-02, FR-B-05, FR-B-10, NFR-13

### US-12 — An operator diagnoses a stalled booking

> As **Noor**, on call, I want to see where a booking stopped and what was attempted, so that I can decide what to do without reading raw logs.

- Given a saga instance, then the operator view lists every step with status, attempts, duration, and any error.
- Given a compensation ran, then it is shown alongside the step it compensated and its outcome.
- Given a saga exhausted its compensation retries, then it appears as `RequiresManualReview` with the failing step highlighted.
- Given a stalled saga older than the recovery timeout, then the recovery worker has already attempted to drive it to a terminal state, and that attempt is visible.

*Satisfies:* FR-B-08, FR-B-11, FR-B-05, NFR-06

### US-13 — A member receives a timely, relevant nudge

> As **Maya**, I want to be told about a trip that fits a gap in my calendar, rather than having to go looking.

- Given a qualifying travel window and eligible inventory, then a nudge is generated carrying its trigger signals and score.
- Given the nudge is actioned, then a fresh quote is produced through the normal pricing path.
- Given the nudge has expired, then it is neither shown nor actionable.
- Given the member dismisses it, then the cooldown suppresses similar nudges for the configured period.

*Satisfies:* FR-O-01, FR-O-04, FR-O-05, FR-O-07, FR-O-09, FR-O-10

### US-14 — Marketing tunes frequency without a deployment

> As **Theo**, I want to change how often members are contacted and prove the engine is not spamming them.

- Given a change to the fatigue cap in partner configuration, then the next engine run honours it with no code change or redeploy.
- Given a member already at the cap, then further nudges are suppressed and recorded with reason `FatigueCapReached`.
- Given a suppression occurred, then it is visible with its reason for review.

*Satisfies:* FR-O-06, FR-O-08, FR-X-07, §6.2

---

## 9. Traceability

| Goal | Requirements | Verified by |
|---|---|---|
| G1 Per-partner pricing | FR-P-01, FR-P-02, FR-X-02 | US-01 |
| G2 Explainability | FR-P-07, FR-P-08 | US-02 |
| G3 No rate leakage | FR-X-05, NFR-04 | US-01, US-07 |
| G4 Ledger correctness | FR-L-01 … FR-L-04, FR-L-10 | US-05 |
| G5 Safe retries | FR-L-05, FR-B-03 | US-03 |
| G6 Exact reversal | FR-L-08 | US-04 |
| G7 Grounded recommendations | FR-C-02 … FR-C-04 | US-06 |
| G8 Recommendation audit | FR-C-05 | US-06, US-09 |
| G9 Tenant isolation | FR-X-02, FR-C-09 | US-07 |
| G10 Enforced architecture | NFR-01, NFR-02 | US-08 |
| G11 Zero-dependency demo | NFR-08, NFR-12, FR-C-07 | US-06 |
| G12 Consistent under failure | FR-B-01, FR-B-05, FR-B-10, NFR-13 | US-11 |
| G13 Ambiguity resolved | FR-B-04, FR-B-06, FR-B-07 | US-11 |
| G14 Operator visibility | FR-B-08, FR-B-11 | US-12 |
| G15 Explainable, capped nudges | FR-O-04 … FR-O-07 | US-13, US-14 |
| G16 Thresholds as configuration | FR-X-07, FR-O-08 | US-14 |

---

## 10. Out of scope for this release

Deferred items are recorded in [06 — Future improvements](06-future-improvements.md) rather than tracked here: multi-currency conversion, cross-partner analytics, real supplier connectivity, partial cancellation and refund policies, promotional stacking rules, fraud detection, role administration screens, real email or push delivery for nudges, and machine-learned ranking.

---

**Next:** [03 — High-level design](03-high-level-design.md)
