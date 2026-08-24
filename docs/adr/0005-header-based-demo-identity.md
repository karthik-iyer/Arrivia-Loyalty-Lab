# ADR-0005 — Header-based demo identity

**Status:** Accepted · **Drives:** FR-X-01, FR-X-03

## Context

Every request needs a partner and, usually, a member. The interesting problems here are *authorization* — tenant isolation, role-based visibility of net rates, cross-tenant denial — not *authentication*.

## Decision

`X-Partner-Code` and `X-Member-Id` headers, plus optional `X-Access-Role` for internal demo identities, resolved by middleware into a `TenantContext` that flows explicitly through the application. A demo switcher in the UI changes them without a re-login.

## Alternatives considered

**ASP.NET Identity with cookies.** Realistic, and adds registration, password storage, and login screens — none of which demonstrate anything about loyalty platforms. It would also make demoing tenant isolation tedious, since comparing two partners' prices side by side would require two browser profiles.

**JWT with a local issuer.** Closer to production shape, and claims would carry tenant and role naturally. Still requires key management and token minting for a property this project deliberately does not claim to have solved.

**External identity provider.** Contradicts NFR-08 outright: no external service.

## Consequences

Accepted: **anyone can claim to be anyone.** This is an intentionally unauthenticated demo, stated plainly in the README so it cannot be mistaken for an oversight, and listed first in [future improvements](../06-future-improvements.md).

Gained: the parts that *are* implemented are the parts that matter. Authorization is real — tenant filters, role-gated fields, cross-tenant requests returning *not found* rather than *forbidden*. Because `TenantContext` is populated in exactly one place, replacing headers with JWT claims later is a middleware change and nothing more.
