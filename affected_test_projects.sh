#!/bin/bash
# The MIT License (MIT)
# Copyright (c) 2026 Stuart Lang
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
#
# Usage: affected_test_projects.sh [base]
#
# Prints, as a JSON array on stdout, the names of the test projects under tests/ whose tests the
# commits since HEAD's merge base with base could affect. ci.yml gates each integration test job
# on its project appearing in that array. The reasoning behind the answer goes to stderr.
#
# dotnet-affected (https://dotnet-affected.com) answers for files that MSBuild projects reference,
# including Directory.Build.props/.targets and package versions in Directory.Packages.props. The
# CI jobs also depend on files no project references - the workflow itself, the docker-compose
# files that stand brokers up - so a change to anything outside the project trees that is not
# known to be documentation selects every test project. Without base, or when dotnet-affected
# fails, every test project is selected too: running tests that did not need to run costs
# minutes, skipping tests that did need to run costs a regression.
set -euo pipefail
export LC_ALL=C

REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
cd "$REPOSITORY_ROOT"

all_test_projects() {
    git ls-files 'tests/*.csproj' | xargs -n1 basename | sed 's/\.csproj$//'
}

is_documentation() {
    case "$1" in
        *.md | docs/* | specs/* | bugfixes/* | images/* | assets/* | .claude/* | .agent_instructions/*) return 0 ;;
        .github/workflows/ci.yml) return 1 ;;
        .github/*) return 0 ;;
        *) return 1 ;;
    esac
}

is_seen_by_dotnet_affected() {
    case "$1" in
        src/* | tests/* | tools/* | samples/* | benchmarks/*) return 0 ;;
        Directory.Build.props | Directory.Build.targets | Directory.Packages.props) return 0 ;;
        *) return 1 ;;
    esac
}

first_change_outside_projects() {
    local file
    while IFS= read -r file; do
        if ! is_documentation "$file" && ! is_seen_by_dotnet_affected "$file"; then
            echo "$file"
            return
        fi
    done < <(git diff --name-only "$1" HEAD)
}

affected_test_projects() {
    local output_dir status=0
    output_dir=$(mktemp -d)
    dotnet tool run dotnet-affected --from "$1" --uncommitted none --format text \
        --output-dir "$output_dir" --output-name affected >&2 || status=$?
    case "$status" in
        0) sed -n "s#^$REPOSITORY_ROOT/tests/.*/\([^/]*\)\.csproj\$#\1#p" "$output_dir/affected.txt" ;;
        166) ;;
        *) rm -rf "$output_dir"; return "$status" ;;
    esac
    rm -rf "$output_dir"
}

to_json_array() {
    local names
    names=$(sort -u | sed '/^$/d; s/.*/"&"/' | paste -sd, -)
    echo "[$names]"
}

if [[ $# -eq 0 ]]; then
    echo "No commit to compare against: selecting every test project." >&2
    all_test_projects | to_json_array
    exit 0
fi

# A branch that has fallen behind its base must not be charged with the base's newer changes.
FROM=$(git merge-base "$1" HEAD)

OUTSIDE=$(first_change_outside_projects "$FROM")
if [[ -n "$OUTSIDE" ]]; then
    echo "$OUTSIDE changed, and no project references it: selecting every test project." >&2
    all_test_projects | to_json_array
    exit 0
fi

if ! AFFECTED=$(affected_test_projects "$FROM"); then
    echo "::warning::dotnet-affected failed: selecting every test project." >&2
    all_test_projects | to_json_array
    exit 0
fi

if [[ -z "$AFFECTED" ]]; then
    echo "No test project is affected by the changes since $FROM." >&2
else
    echo "Test projects affected by the changes since $FROM:" >&2
    echo "$AFFECTED" | sed 's/^/  /' >&2
fi
echo "$AFFECTED" | to_json_array
