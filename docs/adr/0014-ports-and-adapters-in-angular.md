# ADR-0014 — Ports and adapters inside the Angular app

**Status:** Accepted · **Drives:** NFR-09, US-08

## Context

Frontends routinely wire `HttpClient` directly into components, or into services that are HTTP clients wearing a different name. The result is an application whose business logic cannot be exercised without a network mock, and whose components know the shape of the wire.

A backend built on clean architecture with a frontend built that way is a half-finished argument.

## Decision

Apply the same dependency rule in the browser. `domain/` holds models and port tokens (`InjectionToken` from `@angular/core` is the only Angular import there — no HTTP, no RxJS); `application/` holds use cases and stores; `data/` holds HTTP adapters implementing the ports; `core/` binds them once via `provideDataLayer()`. Components inject stores and nothing else.

## Alternatives considered

**Services calling `HttpClient` directly, injected into components.** The Angular default. Components end up coupled to DTO shapes, so a backend field rename ripples into templates, and every component test needs `HttpTestingController`.

**A single generic `ApiService` wrapping HttpClient.** Centralizes the URL handling and nothing else. Callers still work in wire shapes, and the service becomes a grab-bag of unrelated methods.

**Mirror the backend's full layering with separate npm packages per layer.** Maximum enforcement, and build complexity out of proportion to an application of this size. ESLint boundary rules achieve the enforcement that matters.

## Consequences

Accepted: more indirection than a small application strictly needs — a port, an adapter, and a mapper where a single service call would do — and mappers that must be maintained as contracts evolve.

Gained: component tests bind ports to in-memory fakes through the same seam production uses, so no component test touches HTTP. DTO changes are absorbed by mappers, which are unit tested against captured payloads. And the boundary rule is enforced by lint rather than by reviewer memory, which is the difference between an architecture and an intention.
