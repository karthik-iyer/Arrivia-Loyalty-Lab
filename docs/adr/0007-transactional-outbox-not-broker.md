# ADR-0007 — Transactional outbox instead of a message broker

**Status:** Accepted · **Drives:** FR-B-06, FR-B-07, NFR-08

## Context

The saga emits events — booking confirmed, credits burned, compensation completed — that drive downstream work. Writing state to the database and publishing an event are two operations that must not be able to diverge, and a process can die between them.

## Decision

Write outbox rows in the **same transaction** as the state change. A hosted dispatcher polls, delivers at least once with backoff, and moves exhausted messages to a poison table.

## Alternatives considered

**Publish directly after committing.** One line of code, and a lost event whenever the process dies in the gap. This is the bug the outbox pattern exists to prevent, and it is invisible until it happens in production.

**Kafka or RabbitMQ.** What production would use, and the right answer at scale. It requires an installation, which contradicts NFR-08 — and it does not remove the need for an outbox anyway, since the dual-write problem sits between the database and the broker regardless of which broker it is.

**In-memory queue.** No dependency and no durability. Events vanish on restart, which is precisely the scenario F3 is built to survive.

**Database-native change data capture.** Elegant, and unavailable on SQLite.

## Consequences

Accepted: polling adds latency measured in hundreds of milliseconds, and the dispatcher is a single consumer with no partitioning. At-least-once delivery makes handler idempotency mandatory — an obligation documented at the handler interface rather than left to memory.

Gained: no event is ever lost, and the semantics that matter — atomic write-and-enqueue, retry, poison handling — are demonstrated honestly. Because publishing sits behind `IOutbox`, substituting a real broker is an adapter change with the dual-write protection already in place.
