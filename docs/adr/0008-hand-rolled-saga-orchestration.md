# ADR-0008 — Hand-rolled saga orchestration, no workflow engine

**Status:** Accepted · **Drives:** FR-B-01, FR-B-08

## Context

Having decided that checkout is a saga ([ADR-0006](0006-payment-out-of-process-and-saga.md)), something must drive it. Mature options exist: MassTransit state machines, NServiceBus sagas, Temporal, Elsa.

## Decision

Implement the orchestrator directly: an `ISagaStep` contract, a persisted `SagaInstance` state machine, and a loop that advances it.

## Alternatives considered

**MassTransit sagas.** Production-grade and well documented. It also wants a transport, brings a substantial dependency, and — decisively for this project — hides the mechanics inside the framework. A reviewer assessing whether the author understands compensation ordering and ambiguous outcomes would learn only that the author can configure MassTransit.

**Temporal or Durable Task.** The strongest answer for real distributed workflows, with durable execution and replay built in. Requires a server, contradicting NFR-08, and hides even more of the mechanics behind the framework's magic.

**Choreography — each service reacting to events rather than a central orchestrator.** Lower coupling and genuinely appropriate for some domains. The trade is that no single place describes the process, which makes "where did this booking stop?" (FR-B-08, US-12) considerably harder to answer. For a six-step process with a required operator view, orchestration is the better fit.

## Consequences

Accepted: code that a library would otherwise provide — retry policy, backoff, state transitions, recovery scanning — and the corresponding responsibility to get it right. Roughly 400 lines that MassTransit would have supplied.

Gained: the mechanics are visible and reviewable, which is the point of the feature. `SagaInstance` and `SagaStepRecord` are ordinary tables, so the operator view is a query rather than an integration with a framework's internal state. Every retry, timeout, and compensation decision is explicit in code that can be read in one sitting.

**In production, use a library.** This decision is made for a POC whose purpose is to demonstrate understanding; that reasoning does not transfer to a system whose purpose is to take bookings, and [future improvements](../06-future-improvements.md) says so.
