# ADR-0003 — Plain use-case classes instead of a mediator library

**Status:** Accepted · **Drives:** NFR-02

## Context

The application layer needs a consistent shape for use cases. MediatR-style dispatch is close to a default in .NET codebases, and defaults deserve scrutiny.

## Decision

One class per use case implementing `IUseCase<TRequest, TResponse>`, registered in DI and injected directly by endpoints.

## Alternatives considered

**MediatR or a similar in-process mediator.** Gives a uniform pipeline and easy cross-cutting behaviours. It costs navigability: `Send(new QuoteOfferCommand(...))` cannot be followed with *go to definition*, so a reader has to know the convention before they can trace a call. For a codebase whose explicit purpose is to be read and assessed, that is a poor trade. It is also a dependency earning its keep mainly through pipeline behaviours, and this project has few.

**Fat service classes grouping related operations.** Fewer files, but they accumulate dependencies until a class needs six constructor arguments to serve any one method, and unit tests must satisfy all of them regardless.

**Static functions.** Testable and simple, but awkward with constructor injection and inconsistent with the rest of the stack.

## Consequences

Accepted: cross-cutting concerns are applied explicitly — validation in endpoint filters, logging in middleware, transactions in a unit-of-work decorator — rather than materialising from a pipeline. More files, one per operation.

Gained: every call site is navigable by tooling. Constructor dependencies state exactly what a use case needs, so an over-large constructor becomes visible design feedback rather than hidden behind a dispatcher.
