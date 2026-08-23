# ADR-0009 — Deterministic core; the model only narrates

**Status:** Accepted · **Drives:** FR-C-04, FR-C-06, FR-C-07, G7

## Context

An AI concierge in a travel loyalty product touches money, eligibility, and inventory. A hallucinated price is a refund fight; a hallucinated property is a support ticket and a lost customer.

## Decision

Selection, eligibility, affordability, pricing, and ranking are deterministic domain code. A language model, if configured at all, receives the finished result and rephrases it. Its output is validated against the facts, and rejected narration falls back to a template.

## Alternatives considered

**Model with tool calling, free to decide what to show.** The common architecture and the most flexible. It puts a non-deterministic component in the eligibility and pricing path, where the same question can yield different answers and a wrong answer is a financial liability. Tool-calling would also make the audit block (FR-C-05) largely fictional, since the reasoning would live in the model rather than in code.

**Retrieval-augmented generation over an offer index.** Better grounding than raw generation, and still generative at the point where a price is stated. RAG reduces hallucination; it does not eliminate it, and "reduced" is not a standard that applies to money.

**No model at all.** Honest and slightly dull. The narration layer costs little and demonstrates the boundary — which is the actual insight being offered.

## Consequences

Accepted: the concierge cannot handle open-ended conversation, and criteria parsing is keyword-based rather than semantic. Requests outside the vocabulary degrade to a broad search rather than a clever interpretation.

Gained: every price and every offer is real, by construction rather than by evaluation. The system works with no model configured (FR-C-07), so the demo never depends on a key. Recommendations are reproducible, which makes them testable. And the interesting claim — *here is exactly where the model is allowed to operate, and here is the validator that enforces it* — is one this design can actually defend.
