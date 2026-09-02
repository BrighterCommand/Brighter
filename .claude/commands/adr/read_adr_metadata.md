---
allowed-tools: Read, Glob, Grep, Bash(awk:*), Bash(ls:*), Bash(sed:*), Bash(grep:*)
description: Extract ADR frontmatter cheaply and return prior-art candidates filtered by tag/status
argument-hint: [<focus text>] [--tags a,b,c] [--status S,...] [--include-retired] [--limit N]
---

# Read ADR Metadata

You are surfacing **prior-art ADRs** relevant to a focus area by reading only each ADR's YAML
**frontmatter** — never the bodies. This is the *read* half of the ADR metadata pair (the write half
is `write_adr_metadata`). It is deliberately cheap: the frontmatter is a few lines per ADR, so an
agent can scan the whole corpus without spending its context budget on full ADR text.

**Source of truth**: the schema, status vocabulary, tag taxonomy, and identity rule live in
[`.agent_instructions/adr_frontmatter.md`](../../../.agent_instructions/adr_frontmatter.md).

## Arguments

```
$ARGUMENTS
```

Parse `$ARGUMENTS` as an optional free-text **focus** plus optional flags:

| Argument            | Effect |
|---------------------|--------|
| `<focus text>`      | The topic of the new design. Used to rank candidates by relevance. |
| `--tags a,b,c`      | Restrict to ADRs carrying **any** of these tags (drawn from the taxonomy). |
| `--status S,...`    | Restrict to these statuses. Overrides the default retired-skip filter. |
| `--include-retired` | Include `Deprecated` and `Superseded` ADRs (off by default). |
| `--limit N`         | Return at most N candidates (default 8). |

With no focus and no filters, return the full set of in-force ADRs (`Proposed` + `Accepted`).

## Rules

- **Read frontmatter only.** Never open an ADR body to answer this — that defeats the purpose.
- **Skip retired ADRs by default.** Exclude `Deprecated` and `Superseded` unless `--include-retired`
  or an explicit `--status` says otherwise. Retired decisions must not be offered as live prior art.
- **Identity is the `id`/slug, never the bare number** (D6). Key, dedupe, and report every result by
  `id`. Duplicate numbers across ADRs are expected and harmless — do not collapse two ADRs just
  because they share a number.
- Report **paths relative to the repo root** (`docs/adr/<id>.md`).

## Procedure

### Step 1 — Extract every frontmatter block (one cheap call)

Pull the block between the first two `---` fences of every ADR in a single pass:

```bash
awk 'FNR==1{n=0; print "===FILE:" FILENAME} /^---$/{n++; next} n==1{print}' docs/adr/*.md
```

Each `===FILE:<path>` marker is followed by that ADR's frontmatter lines (`id`, `title`, `status`,
`author`, `created`, `summary`, `tags`). Parse this output into records keyed by `id`. Do **not**
Read the ADR files themselves.

### Step 2 — Filter

- Drop any record whose `status` is `Deprecated` or `Superseded`, unless `--include-retired` is set
  or `--status` explicitly lists that status.
- If `--status` is given, keep only those statuses.
- If `--tags` is given, keep only records sharing at least one of those tags.

### Step 3 — Rank

Rank the survivors by relevance to the focus (and `--tags`), most relevant first:

1. Tag overlap with `--tags` or with tags implied by the focus text (strongest signal).
2. Focus terms appearing in `title` / `summary`.
3. `Accepted` ahead of `Proposed` when relevance is otherwise equal (in-force decisions first).

If there is no focus and no `--tags`, order by `id` ascending. Truncate to `--limit` (default 8), and
if you truncated, say how many total matched so the caller knows the list was capped.

### Step 4 — Return candidates

Emit a compact ranked list — this feeds a caller (`/spec:design`, `/adr`), so keep it scannable. For
each candidate give `id`, `title`, `status`, `tags`, `summary`, and the relative `path`:

```
Prior-art ADRs for "<focus>" (showing N of M):

1. 0043-rabbitmq-mutual-tls  [Accepted]  tags: transports, rabbitmq, security
   Adds opt-in client-certificate (mTLS) auth to the RabbitMQ gateway, defaulting to off.
   docs/adr/0043-rabbitmq-mutual-tls.md

2. ...
```

If nothing matches, say so plainly (no prior art found for the focus) rather than returning unrelated
ADRs — a false match is worse than an empty result, because the caller trusts it without opening the
ADR.

> This skill only reads. It never edits frontmatter or the derived `docs/adr/index.md`.

## Examples

```bash
# Prior art before drafting a dead-letter-queue ADR
read_adr_metadata "dead letter queue handling for the outbox" --tags "dead-letter-queue,outbox"

# Everything tagged rabbitmq that is still in force
read_adr_metadata --tags "rabbitmq"

# Include superseded/deprecated too (e.g. to trace a decision's history)
read_adr_metadata "message rejection routing" --include-retired
```
