# Architecture Decision Records

Each record captures one decision, the alternatives that were genuinely considered, and the consequences accepted. Records are immutable once accepted; a reversal is a new record that supersedes the old one.

The value here is the **alternatives** sections. A decision without a rejected alternative was not a decision, it was a default — and defaults are the ones that turn out to be wrong.

| # | Decision | Status | Drives |
|---|---|---|---|
| [0001](0001-clean-architecture-with-vertical-slices.md) | Clean Architecture with vertical feature slices | Accepted | NFR-01, US-08 |
| [0002](0002-single-solution-single-database.md) | One solution, one SQLite database | Accepted | NFR-08 |
| [0003](0003-plain-use-case-classes.md) | Plain use-case classes, no mediator library | Accepted | NFR-02 |
| [0004](0004-result-type-for-expected-failures.md) | `Result<T>` for expected failures | Accepted | FR-X-06, error catalog |
| [0005](0005-header-based-demo-identity.md) | Header-based demo identity | Accepted | FR-X-01, FR-X-03 |
| [0006](0006-payment-out-of-process-and-saga.md) | Payment out of process; checkout as a saga | Accepted | FR-B-01 … FR-B-05 |
| [0007](0007-transactional-outbox-not-broker.md) | Transactional outbox instead of a broker | Accepted | FR-B-06, FR-B-07 |
| [0008](0008-hand-rolled-saga-orchestration.md) | Hand-rolled orchestration, no workflow engine | Accepted | FR-B-01, FR-B-08 |
| [0009](0009-deterministic-core-llm-narration-only.md) | Deterministic core; the model only narrates | Accepted | FR-C-04, FR-C-06, FR-C-07 |
| [0010](0010-mcp-hosted-in-api-process.md) | MCP server hosted in the API process | Accepted | FR-C-08 |
| [0011](0011-derived-balances-not-stored.md) | Balances derived from the ledger | Accepted | FR-L-04 |
| [0012](0012-effective-dated-pricing-rules.md) | Effective-dated rules, never mutated | Accepted | FR-P-03 |
| [0013](0013-signals-and-stores-not-ngrx.md) | Signals and stores, not NgRx | Accepted | NFR-09 |
| [0014](0014-ports-and-adapters-in-angular.md) | Ports and adapters inside the Angular app | Accepted | NFR-09 |
