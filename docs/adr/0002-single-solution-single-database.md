# ADR-0002 — One solution, one SQLite database

**Status:** Accepted · **Drives:** NFR-08, NFR-12

## Context

The project must run from a clean clone on an unfamiliar machine, with no cloud account, no API key, and no container runtime. It also has to be reviewed by someone with limited time who should not spend it on setup.

## Decision

One solution, one SQLite file, migrations and seeding applied automatically at startup.

## Alternatives considered

**PostgreSQL in Docker.** Closer to production and supports features SQLite lacks. It also assumes Docker is installed and running, which is exactly the assumption that turns a five-minute review into a support conversation. EF Core keeps the provider swap small if it is ever wanted.

**In-memory database.** Fastest to start, but state vanishes on restart — which would make the crash-and-recover demonstration in F3 impossible to show, since the whole point is that persisted state survives the process.

**Separate database per feature.** Would model microservice boundaries more faithfully, at the cost of losing the local transactionality that the ledger genuinely relies on. The distributed-consistency problem is still demonstrated, deliberately, at the one boundary where it is real: payment (see [ADR-0006](0006-payment-out-of-process-and-saga.md)).

## Consequences

Accepted: SQLite's type affinity requires explicit configuration for `decimal` and `DateTimeOffset`, its concurrency model is weaker than a server database, and a few production concerns cannot be demonstrated here.

Gained: `git clone` then two commands, and reviewers see the software rather than the setup. Deterministic seeding plus a fixed clock make the demo script reproducible (NFR-12), and tests run against the real provider rather than an in-memory substitute that behaves differently.
