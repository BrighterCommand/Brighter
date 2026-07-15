---
id: 0065-add-frontmatter-to-adrs
title: "Add Frontmatter to ADRs"
status: Proposed
author:
  - "Ian Cooper"
created: 2026-07-15
summary: "Adds YAML frontmatter (id, title, status, author, created, summary, tags) to every ADR so agents can find prior art cheaply, and adopts the filename slug as the stable identity so duplicate ADR numbers are harmless."
tags:
  - "meta"
  - "architecture"
---

# 65. Add Frontmatter to ADRs

Date: 2026-07-15

## Status

Proposed

## Context

Our ADR corpus has grown past eighty records. Increasingly, both automated agents and human
contributors need to find the ADRs relevant to a new design before making a decision — to reuse a
prior choice, to avoid contradicting one, or to supersede one deliberately.

The only way to do that today is to read the ADR bodies, or grep across all of them. Both are
expensive: reading every ADR to answer "have we decided anything about dead-letter queues?" burns a
large amount of an agent's context budget, and grep over full bodies returns noisy matches that
still have to be read to judge relevance. Superseded ADRs make this worse — they are indistinguishable
from live decisions without opening them, so an agent can be led by a decision that has since been
replaced.

There is a second, related problem. ADR numbers were allocated by each branch taking the next number
in sequence (`max + 1`) without any lock. Two branches developed in parallel therefore pick the same
number, and on merge the corpus ends up with several numbers shared by multiple ADRs (37, 38, 39, 53,
57, and others). The bare number is no longer a unique identity, yet tooling and cross-references
have historically treated it as one.

We need a way to describe each ADR with enough metadata to decide whether to read it, and we need a
stable identity for an ADR that does not depend on the number being unique.

## Decision

We will add a YAML **frontmatter** block to every ADR, and treat the filename slug as the ADR's
identity.

### Metadata

Each ADR carries a frontmatter block with: `id`, `title`, `status`, `author`, `created`, `summary`,
and `tags`. The `summary` is the load-bearing field — it is written so that a reader can decide from
the summary alone whether the full ADR is worth opening. `tags` are drawn from a controlled
vocabulary so that filtering by topic is reliable. The full schema, field reference, status vocabulary,
and tag taxonomy live in [`.agent_instructions/adr_frontmatter.md`](../../.agent_instructions/adr_frontmatter.md).

### Status vocabulary

We use the Nygard vocabulary `Proposed` / `Accepted` / `Deprecated` / `Superseded`. The frontmatter
`status` mirrors the body `## Status`. An ADR starts `Proposed`, becomes `Accepted` on approval, is
set `Superseded` when a *specific* later ADR replaces it, and `Deprecated` when it is retired without
a named replacement. Readers looking for prior art skip `Deprecated` and `Superseded` ADRs by default.
Backfilling this metadata onto the existing corpus also normalizes the historically inconsistent
`## Status` prose (values such as `Adopted`, `Modified`, `Proposal`, `Retired`) to these canonical
values.

### Identity: the slug, not the number

The **stable identity of an ADR is its filename slug** (number + kebab-case title), recorded in the
`id` field. The number is retained only as a human-friendly ordering hint. Because identity is the
slug, a number shared by two ADRs is not a defect and requires no correction — the existing duplicate
numbers are left exactly as they are. New ADRs continue to take `max + 1` as a number; a collision
with a concurrent branch is acceptable.

We deliberately do **not** renumber the existing ADRs to make numbers unique. The corpus has many
inbound references — inter-ADR links, `release_notes.md`, `CONTRIBUTING.md`, and the `specs/`
directories — and renumbering would rewrite those and break historical anchors for no gain once
identity no longer depends on the number.

### Access

Reading and writing this metadata is a distinct responsibility from authoring the ADR prose, so it is
allocated to two dedicated skills — one that reads frontmatter to surface prior art, and one that
writes it and enforces the status transitions. A derived index generated from the frontmatter gives a
single-file view for cheap scans.

## Consequences

### Positive

- An agent can decide which ADRs to read from `title` + `summary` + `tags` without opening the
  bodies, cutting token cost and context pollution.
- Superseded decisions are explicitly marked and can be filtered out, so stale decisions stop
  misleading new designs.
- The slug-as-identity rule makes the existing duplicate numbers harmless and removes the pressure to
  renumber, avoiding a large and destructive reference rewrite.

### Negative

- Every ADR now carries a metadata block that must be kept accurate; a wrong `summary` or `status` is
  worse than none because it is trusted without opening the ADR.
- Frontmatter and body `## Status` can drift if both are not updated together; the write skill and the
  approval step are responsible for keeping them in step.
- The bare ADR number remains ambiguous. Prose that refers to "ADR 37" is imprecise; contributors
  must prefer the slug where ambiguity is possible.

### Risks and Mitigations

- **Risk**: the derived index drifts from the frontmatter and is silently trusted.
  - **Mitigation**: the index is generated from frontmatter and never hand-edited, so it can always
    be regenerated to the correct state.
- **Risk**: backfilled `summary`/`tags` are low quality across eighty-plus ADRs.
  - **Mitigation**: tags are constrained to a controlled vocabulary, and legacy status prose is
    normalized through an explicit mapping with ambiguous cases flagged for human confirmation rather
    than guessed.

## Alternatives Considered

**Renumber the ADRs so numbers are unique.** Rejected: the reference and historical-anchor cost is
high, and once identity is the slug the number's uniqueness no longer matters.

**A single hand-maintained index/table instead of per-ADR frontmatter.** Rejected: a maintained index
is a separate artifact that drifts whenever an author forgets to update it. Per-ADR frontmatter keeps
the metadata next to the decision it describes, and the index is derived from it rather than authored.

**Use a `Draft` / `Approved` status vocabulary** (as first sketched in the originating issue).
Rejected in favour of keeping Nygard's `Proposed` / `Accepted`, which the existing ADRs and the
approval workflow already use, extended only with `Superseded`.

## References

- Issue: [#4234 Add Frontmatter to our ADRs](https://github.com/BrighterCommand/Brighter/issues/4234)
- Conventions: [`.agent_instructions/adr_frontmatter.md`](../../.agent_instructions/adr_frontmatter.md)
- Related ADRs: [0001-record-architecture-decisions](0001-record-architecture-decisions.md) — the
  original decision to use ADRs, which this extends with metadata.
