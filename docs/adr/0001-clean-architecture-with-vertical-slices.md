# ADR-0001 — Clean Architecture with vertical feature slices

**Status:** Accepted · **Drives:** NFR-01, US-08

## Context

Five features share a domain and depend on one another. The codebase must stay navigable as they accumulate, and the pricing and ledger rules must be testable without a database, a clock, or HTTP.

Two organizing principles were available, and they are frequently presented as rivals.

## Decision

Use both. **Layers** govern dependency direction; **slices** govern placement.

Projects form the layers — `Domain`, `Application`, `Infrastructure`, `Api` — with dependencies pointing inward only. Inside each project, folders form the slices: `Pricing`, `Loyalty`, `Booking`, `Concierge`, `Opportunity`.

A developer changing pricing opens `Pricing` in three projects and nothing else. A developer who accidentally imports `Infrastructure` from `Domain` gets a failing build.

## Alternatives considered

**Layers only (technical folders: `Services`, `Repositories`, `Models`).** Dependency direction is clear, but a feature is smeared across the tree. Finding everything that pricing touches becomes an exercise in grep, and slices grow coupled because there is no boundary discouraging it.

**Vertical slices only, each owning its own persistence.** Excellent isolation, and genuinely appropriate when slices are independent. These are not: the concierge needs pricing and the ledger, and the saga needs both. Duplicating a `Money` type per slice — or worse, letting each slice define its own credit semantics — trades a real coupling problem for an integration problem that is harder to see.

**Layers with runtime module boundaries (separate assemblies per feature).** Stronger enforcement, more project overhead than five co-dependent features justify.

## Consequences

Accepted: more projects than a small POC strictly needs, and interfaces defined in `Application` while implemented in `Infrastructure`, which requires one mental hop when reading a call chain.

Gained: `Domain.Tests` runs with no database and no mocking framework. Architecture rules are executable — `NetArchTest` asserts them and fails the build (NFR-01), so the claim in this document cannot quietly become false.
