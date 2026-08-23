# ADR-0006 — Payment out of process; checkout as a saga

**Status:** Accepted · **Supersedes:** the single-transaction checkout in an earlier draft · **Drives:** FR-B-01 … FR-B-05, G12, G13

## Context

An earlier version of this design confirmed bookings inside one database transaction, with payment simulated in process. That was correct for what it was, and it was honest about being a simplification — but it also meant Feature 3 demonstrated nothing that a transaction does not already give for free.

Real checkout spans a supplier, a payment processor, a ledger, and a booking record. No transaction spans those. The failure that matters is not "the payment declined" — that is easy. It is "the payment call timed out and I do not know whether it succeeded."

## Decision

Run the payment simulator as a **separate process** reached over HTTP, and orchestrate checkout as a saga with persisted state, derived idempotency keys, explicit compensations, and an `Unknown` step outcome resolved by querying the far side.

## Alternatives considered

**Keep payment in process, simulate latency and failure with a delay and a coin flip.** Cheaper, and superficially similar. It cannot produce a genuinely ambiguous outcome: an in-process call that throws always tells you it failed. The one branch worth building — resolving *unknown* — would be untestable, and the saga would be ceremony around a transaction.

**Two databases with a two-phase commit.** Distributed transactions are the textbook answer and the wrong one: no real payment provider offers a prepare phase, and coordinator failure introduces worse problems than it solves.

**Optimistic booking with nightly reconciliation.** How some real systems actually work, and defensible at scale. It moves the interesting logic into a batch job and leaves the member's immediate experience inconsistent — the opposite of what this feature is meant to show.

## Consequences

Accepted: a third process to start (mitigated by `scripts/run-all.ps1`), real network flakiness in tests, and materially more code than a transaction — orchestrator, step contracts, compensations, recovery worker, outbox.

Gained: the failure modes are real, so the tests are meaningful. `Resilience.Tests` can kill the host mid-saga and assert that exactly one authorization exists at the simulator afterwards — a claim that is simply unavailable with an in-process fake. Ledger operations remain locally transactional, so FR-L-07 still holds; the saga governs only what crosses the boundary.
