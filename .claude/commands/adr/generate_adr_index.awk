#!/usr/bin/awk -f
# Regenerate docs/adr/index.md from ADR frontmatter.
# Reads only the YAML frontmatter block (between the first two `---` fences) of
# each ADR; never the bodies. Emits a Markdown table ordered by id.
# Usage: awk -f .claude/commands/adr/generate_adr_index.awk docs/adr/[0-9]*.md > docs/adr/index.md

function trim(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
function unquote(s) { s = trim(s); gsub(/^"|"$/, "", s); return s }
function cell(s) { gsub(/\|/, "\\|", s); gsub(/\n/, " ", s); return s }

FNR == 1 {
    n = 0
    id = ""; title = ""; status = ""; summary = ""; tags = ""; intags = 0
}
/^---[ \t]*$/ {
    n++
    if (n == 2) {             # closing fence: flush this file's record
        intags = 0
        if (id != "") {
            rows[++count] = "| `" id "` | " cell(title) " | " status " | " cell(tags) " | " cell(summary) " |"
        }
    }
    next
}
n != 1 { next }               # only inside the frontmatter block

/^id:/      { id = trim(substr($0, index($0, ":") + 1)); intags = 0; next }
/^title:/   { title = unquote(substr($0, index($0, ":") + 1)); intags = 0; next }
/^status:/  { status = trim(substr($0, index($0, ":") + 1)); intags = 0; next }
/^summary:/ { summary = unquote(substr($0, index($0, ":") + 1)); intags = 0; next }
/^tags:/    { intags = 1; next }
/^[a-zA-Z_]+:/ { intags = 0; next }   # any other top-level key ends the tags list
intags && /^[ \t]*-[ \t]*/ {
    t = $0; sub(/^[ \t]*-[ \t]*/, "", t); t = unquote(t)
    tags = (tags == "" ? t : tags ", " t)
    next
}

END {
    print "<!-- GENERATED FILE - DO NOT EDIT BY HAND."
    print "     Regenerated from ADR frontmatter by /adr and /spec:approve"
    print "     (see .agent_instructions/adr_frontmatter.md). Edit the ADRs, not this file. -->"
    print ""
    print "# Architecture Decision Records — Index"
    print ""
    print "Derived index of every ADR in `docs/adr/`, generated from each file's YAML frontmatter."
    print "It is a regenerable cache: **do not hand-edit**. To refresh it after adding or changing an"
    print "ADR, rerun the generator documented in"
    print "[`.agent_instructions/adr_frontmatter.md`](../../.agent_instructions/adr_frontmatter.md)."
    print ""
    print "Identity is the `id` (filename stem), not the leading number — numbers may repeat (see D6)."
    print ""
    print "| id | Title | Status | Tags | Summary |"
    print "|----|-------|--------|------|---------|"
    for (i = 1; i <= count; i++) print rows[i]
    print ""
    print "_" count " ADRs indexed._"
}
