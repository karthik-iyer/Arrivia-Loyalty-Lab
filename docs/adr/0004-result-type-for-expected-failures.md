# ADR-0004 — `Result<T>` for expected failures

**Status:** Accepted · **Drives:** error catalog, NFR-02

## Context

Much of this domain's behaviour *is* its failure behaviour. An expired quote, an exceeded burn cap, an ineligible offer, and a declined payment are not exceptional — they are outcomes the business defines and the tests exist to verify.

## Decision

Expected failures return `Result<T>` carrying an `Error(Code, Message, Details)`. Exceptions are reserved for defects and infrastructure faults.

## Alternatives considered

**Exceptions for everything.** Idiomatic in much of .NET and requires no new type. But the failure modes disappear from signatures: a caller reading `Task<Quote> QuoteAsync(...)` has no way to know that six business failures are possible without opening the implementation. Exceptions are also expensive on paths that are ordinary rather than exceptional, and `catch (Exception)` around business logic quietly swallows real defects.

**Nullable returns.** Communicates *that* something failed, never *why*. `QUOTE_EXPIRED` and `BURN_CAP_EXCEEDED` need to reach the user as different, actionable messages.

**Out parameters or tuples.** Works, but composes badly and has no `Map`/`Bind`, so multi-step flows degenerate into nested conditionals.

## Consequences

Accepted: callers must handle both branches, which is verbose in places; and discipline is required to keep exceptions from creeping back in for business outcomes.

Gained: failure modes are visible in signatures. Error codes map directly to RFC 7807 responses and to specific frontend affordances ([frontend design §8](../05-frontend-design.md#8-error-handling)), so an expired quote renders a **Get a new price** button rather than a generic red banner. Tests assert on codes rather than exception types or message strings.
