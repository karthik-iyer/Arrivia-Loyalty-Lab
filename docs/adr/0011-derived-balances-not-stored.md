# ADR-0011 — Balances derived from the ledger

**Status:** Accepted · **Drives:** FR-L-04, G4

## Context

A member's credit balance is read on nearly every page. The obvious optimization is a `Balance` column updated on each transaction.

## Decision

No stored balance. The balance is the sum of a member's ledger entries, and the ledger is the only source of truth.

## Alternatives considered

**Stored balance column updated transactionally.** Fast reads, and a second source of truth. Any code path that writes an entry without updating the column — a migration, a manual correction, a bug in a rarely used branch — produces a balance that disagrees with its own history. Because both numbers look authoritative, the disagreement is typically discovered by a member rather than by the team, and reconstructing which one is right means replaying the ledger anyway.

**Materialized view.** The database maintains consistency, which removes the drift risk. SQLite has no materialized views, and the abstraction would need reimplementing per provider.

**Periodic snapshots as the read path.** Faster on long histories, at the cost of a staleness window on a number used for authorization decisions. A member could burn credits they no longer have.

## Consequences

Accepted: balance reads scale with entry count. At demo volumes this is microseconds; at production volumes it needs attention.

Gained: the balance cannot be wrong. Invariant #4 — issued minus burned minus expired equals outstanding — holds by construction rather than by convention, which is what allows the property-based test to assert it over randomized transaction sequences.

FR-L-13 leaves room for periodic snapshots as an **accelerator**, explicitly not as the source of truth: a snapshot plus subsequent entries, with the ledger still authoritative. That keeps the optimization available without reintroducing the second version of the truth.
