# ADR-0013 — Signals and stores, not NgRx

**Status:** Accepted · **Drives:** NFR-09

## Context

The Angular application has genuine state: session and tenant, catalog results and filters, a quote in progress, a live saga timeline, wallet history, concierge results, and an inbox.

## Decision

Injectable stores holding `signal()` state, exposing `asReadonly()` accessors and `computed()` projections. Components read signals and call methods.

## Alternatives considered

**NgRx.** Strong conventions, excellent tooling, and genuinely valuable on large teams. It also costs four files per feature — actions, reducer, effects, selectors — and this application's state is mostly request-scoped rather than globally shared. Time-travel debugging is a real benefit that this application does not need.

**Plain services with `BehaviorSubject`.** The pre-signals idiom. Works, but requires `async` pipes throughout, careful subscription management, and manual change-detection discipline. Signals supersede it on all three counts.

**Component-local state only.** Simplest, and unworkable: session and theme are cross-cutting, and the checkout store must survive route-level component churn while a saga polls.

## Consequences

Accepted: no time-travel debugging, and store discipline is a convention rather than something a framework enforces. Guarded by review and by the ESLint boundary rules in [frontend design §1](../05-frontend-design.md#1-layers).

Gained: substantially less code per feature, `OnPush` change detection that works naturally rather than by careful arrangement, and derived state as `computed()` — so the checkout `outcome` is a projection of the booking and cannot contradict it. Stores are plain classes, so testing them needs no store-testing library, only fake ports.

Checkout-scoped stores are provided at the route rather than in root, so navigating away disposes the state instead of carrying a stale booking into the next attempt.
