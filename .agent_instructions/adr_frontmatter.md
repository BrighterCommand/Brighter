# ADR Frontmatter Conventions

This document defines the YAML frontmatter that every Architecture Decision Record (ADR) in
`docs/adr/` carries. It is the single source of truth referenced by the `read_adr_metadata` and
`write_adr_metadata` skills, by `/spec:design` and `/adr`, and by the backfill process that added
frontmatter to the existing ADRs.

## Why frontmatter

Agents (and people) need to find prior ADRs relevant to a new design without reading every ADR in
full. Grepping whole ADR bodies is expensive in tokens and pollutes context — especially with
superseded decisions. A small, structured metadata block at the top of each ADR lets a reader scan
`title` + `summary` + `tags` cheaply and decide which ADRs are worth opening.

See ADR *"Add Frontmatter to ADRs"* for the decision record behind this convention.

## Schema

Frontmatter is a YAML block delimited by `---` fences, placed at the very top of the file, before
the `# N. Title` heading:

```yaml
---
id: 0043-rabbitmq-mutual-tls
title: "Optional Mutual TLS (mTLS) Support for RabbitMQ Messaging Gateway"
status: Accepted
author:
  - "Ian Cooper"
created: 2026-06-18
summary: "Adds opt-in client-certificate (mTLS) authentication to the RabbitMQ gateway via connection configuration, defaulting to off so existing deployments are unaffected."
tags:
  - "transports"
  - "rabbitmq"
  - "security"
---

# 43. Optional Mutual TLS (mTLS) Support for RabbitMQ Messaging Gateway

Date: 2026-06-18
...
```

## Field reference

| Field     | Required | Type            | Notes |
|-----------|----------|-----------------|-------|
| `id`      | yes      | string          | **Stable identity = the filename stem** (name without `.md`), e.g. `0043-rabbitmq-mutual-tls`. This is the unique key; see *Identity and numbering* below. |
| `title`   | yes      | string (quoted) | The ADR title. Use the H1 title text (without the leading `N.` number). |
| `status`  | yes      | enum            | One of `Proposed`, `Accepted`, `Deprecated`, `Superseded`. Mirrors the body `## Status`. See *Status* below. |
| `author`  | yes      | list of strings | Always list form, even for one author: `- "Name"`. Backfill default is `- "Brighter Team"` where no author is recorded. |
| `created` | yes      | date (ISO 8601) | `YYYY-MM-DD`. For existing ADRs use the body `Date:` line. |
| `summary` | yes      | string (quoted) | One or two sentences describing the decision. This is what an agent reads to decide whether to open the full ADR — make it specific about *what was decided*, not just the topic. |
| `tags`    | yes      | list of strings | 1–4 topic tags drawn from the controlled vocabulary below. |

## Identity and numbering

The ADR **number is a non-unique ordering hint, not an identity.** ADR numbers were historically
allocated by each branch picking `max+1` without locking, so parallel branches collided: several
numbers (37, 38, 39, 53, 57, …) are shared by multiple ADRs. We do **not** renumber — the inbound
references (inter-ADR links, `release_notes.md`, `CONTRIBUTING.md`, `specs/`) and historical anchors
make renumbering costly and destructive.

Instead:

- The **identity is the `id` field = the filename stem** (number + slug), which *is* unique.
- Tooling (`read_adr_metadata`, the generated index) keys off `id`/slug, never the bare number.
- New ADRs still take `max+1` as a number for ordering, and a number collision with a concurrent
  branch is **acceptable** — it is not a defect to fix, because identity is the slug.

When you refer to an ADR in prose or `Related ADRs`, prefer the slug/filename
(`0043-rabbitmq-mutual-tls`) over the bare number where ambiguity is possible.

## Status

Canonical values (Nygard vocabulary):

- **Proposed** — drafted, not yet approved. New ADRs start here (set by `/spec:design` / `/adr`).
- **Accepted** — approved and in force. `/spec:approve design` flips `Proposed` → `Accepted` in
  both the frontmatter `status` and the body `## Status`.
- **Deprecated** — no longer in force, but *not* replaced by a specific ADR (the decision was
  retired/abandoned). Use this when there is no superseding ADR to point at.
- **Superseded** — replaced by a *specific* later ADR. When a new ADR supersedes an older one, set
  the older ADR's `status` to `Superseded` and add a `Superseded by` reference to the superseding ADR.

`read_adr_metadata` skips both `Deprecated` and `Superseded` ADRs by default when surfacing prior art.

### Backfill status normalization

The legacy ADR bodies use inconsistent status prose. Backfill normalizes to the canonical values in
**both** the frontmatter `status` **and** the body `## Status` line (this is an intentional cleanup):

| Body text (legacy)                                                  | Canonical `status` |
|---------------------------------------------------------------------|--------------------|
| `Accepted`, `Adopted`, `Approved`, `Modified`, `Accepted. Amended …`| `Accepted`         |
| `Proposed`, `Proposal`, `Draft`, `Proposed (Draft PR …)`            | `Proposed`         |
| `Retired`                                                           | `Deprecated`       |
| An ADR explicitly replaced by a *specific* later ADR                | `Superseded`       |

Normalization rules:
- Where a status line carries extra information (e.g. `Accepted. Amended 2026-02-17 to add Transport
  Nack (see Decision §6).`), normalize the status **word** to the canonical value but preserve the
  amendment sentence in the body (move it below the status line if needed — do not lose it).
- **Partial** supersession (a later ADR supersedes only a *section* of an older one, e.g. ADR 0057
  supersedes `0053 §7`) does **not** change the older ADR's status — it stays `Accepted`; note the
  partial supersession in prose instead.
- Do **not** guess. Only assign `Superseded` when a specific replacing ADR exists; only assign
  `Deprecated` for a genuinely retired decision. Flag anything unclear for human confirmation.

## Tag taxonomy (controlled vocabulary)

Tags are lowercase kebab-case, quoted, 1–4 per ADR, drawn from this controlled list. Prefer the
most specific applicable tags. If no existing tag fits, propose an addition to this list rather than
inventing an ad-hoc tag (keeps the vocabulary from sprawling).

**Architecture & API**
`architecture`, `cqrs`, `mediator`, `pipeline`, `middleware`, `aop`, `dsl`, `api-design`,
`configuration`, `di`, `lifetime`, `specification-pattern`, `reactive`, `workflow`

**Messaging**
`messaging`, `message-pump`, `message-mapping`, `cloudevents`, `claim-check`, `bulk-messaging`,
`publish`, `dispatcher`, `scheduling`, `request-context`, `request-validation`

**Reliability & error handling**
`resilience`, `retry`, `circuit-breaker`, `dead-letter-queue`, `error-handling`,
`message-rejection`

**Storage & boxes**
`outbox`, `inbox`, `box`, `archive`, `provisioning`, `migration`, `locking`, `multi-tenancy`,
`serialization`

**Concurrency & IO**
`concurrency`, `async`, `reactor`, `proactor`, `performance`, `memory`

**Observability**
`observability`, `otel`, `metrics`, `logging`, `tracing`

**Transports & infrastructure**
`transports`, `aws`, `sqs`, `sns`, `dynamo`, `rabbitmq`, `kafka`, `redis`, `mssql`, `postgres`,
`sqlite`, `rocketmq`, `mqtt`, `security`, `tls`

**Process & tooling**
`meta`, `testing`, `test-generation`, `roslyn-analyzers`

## Skills

- **`read_adr_metadata`** — extracts frontmatter blocks cheaply and returns matching ADRs
  (`id` + `title` + `summary` + path) filtered by tag/status. Suggested from `/spec:design` to find
  prior art before drafting a new ADR. Skips `Superseded` by default.
- **`write_adr_metadata`** — adds or updates a file's frontmatter and enforces the status rules
  (new = `Proposed`; supersession sets the old ADR to `Superseded`). Idempotent.

## Derived index

`docs/adr/index.md` is a table generated *from* the frontmatter (id, title, status, tags, summary).
It is a derived cache for single-file reads — it must never be hand-edited; regenerate it from
frontmatter so it cannot drift. The generated file carries a `DO NOT EDIT` banner.

**Regenerate** it after adding an ADR or changing any frontmatter (status, tags, summary, title).
This is the single canonical command — `/adr` and `/spec:approve` call it, and you can run it by
hand:

```bash
awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md
```

The generator reads **only** the frontmatter block of each ADR (like `read_adr_metadata`), keys rows
off `id`, and orders by filename. It is deterministic: rerunning it with no ADR changes produces no
diff, so a dirty `index.md` after regeneration means an ADR's frontmatter actually changed.
