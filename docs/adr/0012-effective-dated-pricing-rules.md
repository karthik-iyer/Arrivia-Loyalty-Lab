# ADR-0012 — Effective-dated rules, never mutated

**Status:** Accepted · **Drives:** FR-P-03, FR-P-07, G2

## Context

Partners change markups, launch campaigns, and adjust tier benefits. Months later, someone asks why a specific booking was priced the way it was — usually because a partner is disputing it.

## Decision

Pricing rules are rows with `EffectiveFrom` (inclusive) and `EffectiveTo` (exclusive). Changing a rule means closing the current row and inserting a new one. Rules are never updated in place and never deleted.

Pricing evaluates against `asOf`, which is the quote's creation timestamp — not "now".

## Alternatives considered

**Mutable rules with an audit table.** Familiar, and it records that a change happened. Reconstructing the rule *set* in force on a given date means replaying an audit log in order and hoping every mutation path wrote to it. The reconstruction is exactly the operation that matters, and it is the one this design makes hardest.

**Event sourcing the entire pricing configuration.** Full history and time-travel by design. Considerably more machinery than a validity window provides, for a bounded problem that a validity window solves completely.

**Snapshot the rules onto each quote.** Guarantees reproducibility for quotes, and answers only questions about quotes. "What would this offer have cost a Silver member last March?" — a question account managers genuinely ask — becomes unanswerable.

## Consequences

Accepted: every rule query carries an `asOf` predicate, the rules table grows monotonically, and overlapping windows for the same scope are possible and must be prevented by the precedence comparator being total.

Gained: any historical price is reproducible exactly, which is what makes the explanation feature trustworthy rather than merely present. Scheduling a future campaign is an insert, not a deployment or a cron job. Rolling one back is closing a row, which is safe because nothing was overwritten.
