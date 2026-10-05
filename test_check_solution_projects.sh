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
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD_SCRIPT="${1:-$SCRIPT_DIR/check_solution_projects.sh}"
TEST_ROOT=$(mktemp -d "${TMPDIR:-/tmp}/brighter-solution-tests.XXXXXX") || exit 1
trap 'rm -rf -- "$TEST_ROOT"' EXIT
PASS=0
FAIL=0
CASES=0

arrange() {
    CASE_NAME="$1"
    CASE_DIR=$(mktemp -d "$TEST_ROOT/case.XXXXXX") || exit 1
    git -C "$CASE_DIR" init --quiet || exit 1
    CASES=$((CASES + 1))
}

write_project() {
    mkdir -p "$CASE_DIR/$(dirname "$1")" || exit 1
    printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$CASE_DIR/$1"
    if [[ "${2:-tracked}" == tracked ]]; then
        git -C "$CASE_DIR" add -- "$1" || exit 1
    fi
}

write_solution() {
    {
        printf '<Solution>\n'
        for project in "$@"; do
            printf '  <Project Path="%s" />\n' "$project"
        done
        printf '</Solution>\n'
    } > "$CASE_DIR/Brighter.slnx"
}

write_exclusions() {
    mkdir -p "$CASE_DIR/.github" || exit 1
    printf '%s\n' "$@" > "$CASE_DIR/.github/solution-project-exclusions.txt"
}

run_guard() {
    (cd "$CASE_DIR" && bash "$GUARD_SCRIPT") > "$CASE_DIR/output" 2>&1
    STATUS=$?
    OUTPUT=$(cat "$CASE_DIR/output")
}

assert_equal() {
    if [[ "$1" == "$2" ]]; then
        PASS=$((PASS + 1))
    else
        echo "FAIL: $CASE_NAME: expected '$1', got '$2'"
        echo "$OUTPUT"
        FAIL=$((FAIL + 1))
    fi
}

assert_contains() {
    if [[ "$OUTPUT" == *"$1"* ]]; then
        PASS=$((PASS + 1))
    else
        echo "FAIL: $CASE_NAME: missing '$1'"
        echo "$OUTPUT"
        FAIL=$((FAIL + 1))
    fi
}

# Arrange / Act / Assert: each case runs the real script in a temporary Git repository.
arrange 'all tracked projects are registered'
projects=()
for folder in benchmarks samples src tests tools; do
    projects+=("$folder/Example.csproj")
    write_project "$folder/Example.csproj"
done
write_solution "${projects[@]}"
run_guard
assert_equal 0 "$STATUS"

for folder in benchmarks samples src tests tools bugfixes; do
    arrange "missing tracked project under $folder"
    write_project "$folder/Missing.csproj"
    write_solution
    run_guard
    assert_equal 1 "$STATUS"
    assert_contains "$folder/Missing.csproj"
done

arrange 'untracked projects and build artifacts are ignored'
write_project 'src/Example.csproj'
write_solution 'src/Example.csproj'
for project in scratch/Untracked.csproj src/bin/Copied.csproj src/obj/Copied.csproj; do
    write_project "$project" untracked
done
run_guard
assert_equal 0 "$STATUS"

arrange 'solution path casing differs from Git'
write_project 'src/Example.AWS/Example.csproj'
write_solution 'src/Example.Aws/Example.csproj'
run_guard
assert_equal 1 "$STATUS"
assert_contains 'src/Example.AWS/Example.csproj'

arrange 'project paths contain spaces'
write_project 'samples/Example App/Example App.csproj'
write_solution 'samples/Example App/Example App.csproj'
run_guard
assert_equal 0 "$STATUS"

arrange 'standalone project is explicitly excluded'
write_project 'bugfixes/diagnostic/Diagnostic.csproj'
write_solution
write_exclusions '# Standalone diagnostic, run manually.' '' 'bugfixes/diagnostic/Diagnostic.csproj'
run_guard
assert_equal 0 "$STATUS"

arrange 'exclusion does not match a tracked project'
write_project 'src/Example.csproj'
write_solution 'src/Example.csproj'
write_exclusions 'bugfixes/Removed.csproj'
run_guard
assert_equal 1 "$STATUS"
assert_contains 'bugfixes/Removed.csproj'

arrange 'excluded project is now registered'
write_project 'src/Example.csproj'
write_solution 'src/Example.csproj'
write_exclusions 'src/Example.csproj'
run_guard
assert_equal 1 "$STATUS"
assert_contains 'src/Example.csproj'

arrange 'solution references an untracked project'
write_solution 'samples/Removed.csproj'
run_guard
assert_equal 1 "$STATUS"
assert_contains 'samples/Removed.csproj'

arrange 'solution is missing'
run_guard
assert_equal 1 "$STATUS"
assert_contains 'Brighter.slnx'

arrange 'solution is incomplete'
printf '<Solution>\n' > "$CASE_DIR/Brighter.slnx"
run_guard
assert_equal 1 "$STATUS"
assert_contains 'Brighter.slnx'

echo "$CASES cases: $PASS assertions passed, $FAIL failed."
[[ "$FAIL" -eq 0 ]]
