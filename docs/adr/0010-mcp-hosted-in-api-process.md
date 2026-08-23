# ADR-0010 — MCP server hosted in the API process

**Status:** Accepted · **Drives:** FR-C-08, US-09

## Context

The concierge capability should be reachable by an external AI agent over the Model Context Protocol, under exactly the same eligibility, affordability, and tenant constraints as the web application.

## Decision

Host the MCP endpoint inside `LoyaltyLab.Api`, with tools implemented as thin adapters over the same use case classes the REST endpoints call.

## Alternatives considered

**A separate MCP server project.** Cleaner deployment separation and independent scaling. It would need its own access to the application layer, which means either sharing the database — inviting two paths to the same invariants — or calling the REST API, adding a hop and a second place for tenant resolution to drift. Two implementations of the same rule is how the two implementations eventually disagree.

**MCP as the only interface, with the web app consuming it.** Interesting and unconventional. MCP is designed for agent interaction, not for a browser client, and shaping every screen around tool-call semantics would add friction with no benefit.

**Skip MCP; expose OpenAPI and let agents use that.** Reasonable, and agents do consume REST. It would forgo demonstrating a protocol that Arrivia is publicly building on, which is precisely the point of including it.

## Consequences

Accepted: the API process serves two protocols, and MCP tool schemas must be maintained alongside REST contracts.

Gained: one implementation of every business rule. A tenant isolation fix applies to both surfaces at once, and an integration test asserts that the MCP tool and the REST endpoint return equivalent results for identical inputs — so the two cannot silently diverge. An architecture test keeps the adapters thin by failing if anything under `Api/Mcp` contains conditional business logic.
