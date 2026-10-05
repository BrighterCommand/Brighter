#!/bin/bash
# The MIT License (MIT)
# Copyright (c) 2026 Irakli Gabisonia
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.
set -euo pipefail
export LC_ALL=C

REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
cd "$REPOSITORY_ROOT"
if [[ ! -r Brighter.slnx ]]; then
    echo 'Cannot read Brighter.slnx.' >&2
    exit 1
fi

if ! grep -Eq '<Solution([[:space:]>])' Brighter.slnx ||
    ! grep -Eq '</Solution>|<Solution[^>]*/>' Brighter.slnx; then
    echo 'Brighter.slnx is missing a complete Solution element.' >&2
    exit 1
fi

CHECK_TEMP=$(mktemp -d "${TMPDIR:-/tmp}/brighter-solution-check.XXXXXX")
trap 'rm -rf -- "$CHECK_TEMP"' EXIT

git -c core.quotePath=false ls-files -- '*.csproj' | sort -u > "$CHECK_TEMP/tracked"
{ grep -o 'Path="[^"]*\.csproj"' Brighter.slnx || [[ $? -eq 1 ]]; } |
    sed 's/Path="//;s/"$//' | sort -u > "$CHECK_TEMP/registered"

EXCLUSIONS='.github/solution-project-exclusions.txt'
if [[ -f "$EXCLUSIONS" ]]; then
    awk '{
        gsub(/^[[:space:]]+|[[:space:]]+$/, "")
        if ($0 != "" && $0 !~ /^#/) print
    }' "$EXCLUSIONS" | sort -u > "$CHECK_TEMP/excluded"
else
    : > "$CHECK_TEMP/excluded"
fi

comm -23 "$CHECK_TEMP/excluded" "$CHECK_TEMP/tracked" > "$CHECK_TEMP/stale-exclusions"
if [[ -s "$CHECK_TEMP/stale-exclusions" ]]; then
    echo 'Exclusions do not match tracked projects:' >&2
    cat "$CHECK_TEMP/stale-exclusions" >&2
    exit 1
fi

comm -23 "$CHECK_TEMP/tracked" "$CHECK_TEMP/excluded" > "$CHECK_TEMP/expected"
if ! diff -u -L 'Git-tracked projects (minus explicit exclusions)' -L 'Brighter.slnx projects' \
    "$CHECK_TEMP/expected" "$CHECK_TEMP/registered"; then
    echo 'Project lists differ. Check Brighter.slnx and .github/solution-project-exclusions.txt.' >&2
    exit 1
fi

echo 'Solution project check passed.'
