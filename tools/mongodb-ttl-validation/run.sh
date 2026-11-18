#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'HELP'
Usage: ./tools/mongodb-ttl-validation/run.sh [--ttl-only] [--keep]

Runs the TTL regressions and full MongoDB suite on .NET 9 and .NET 10 in Docker.
  --ttl-only  Run only MongoDbCollectionTtlIndexTests on both frameworks.
  --keep      Keep this run's databases for inspection; cleanup.sh is saved with results.
  --help      Show this help.

Requires Docker with Compose v2+ and internet access for images and NuGet restore.
Results are saved under artifacts/mongodb-ttl/<run-id>/ in this checkout.
No host ports are published. The checkout is mounted read-only.
Uses Docker's existing bridge; no additional subnet is allocated.
HELP
}

scope=full
keep=0
for arg in "$@"; do
    case "$arg" in
        --ttl-only) scope=ttl ;;
        --keep) keep=1 ;;
        --help|-h) usage; exit 0 ;;
        *) printf 'Unknown argument: %s\n' "$arg" >&2; usage >&2; exit 2 ;;
    esac
done

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo_dir=$(cd -- "$script_dir/../.." && pwd)
command -v docker >/dev/null || { printf 'Docker is not installed.\n' >&2; exit 1; }
docker compose version >/dev/null
if ! docker info >/dev/null 2>&1; then
    printf 'Docker is not accessible. Start Docker Desktop or the Docker daemon, then retry.\n' >&2
    exit 1
fi

run_id="$(date -u +%Y%m%dt%H%M%Sz)-$$"
project_name="brighter-ttl-4717-$run_id"
results="$repo_dir/artifacts/mongodb-ttl/$run_id"
mkdir -p "$results"
export BRIGHTER_TTL_SOURCE="$repo_dir"
export BRIGHTER_TTL_RESULTS="$results"
export BRIGHTER_TTL_SCOPE="$scope"
export BRIGHTER_TTL_UID
export BRIGHTER_TTL_GID
BRIGHTER_TTL_UID=$(id -u)
BRIGHTER_TTL_GID=$(id -g)
export BRIGHTER_TTL_MONGO_IMAGE="${BRIGHTER_TTL_MONGO_IMAGE:-mongo:7}"
compose=(docker compose --project-name "$project_name" --file "$script_dir/compose.yaml")

{
    printf '#!/usr/bin/env bash\nset -euo pipefail\n'
    for name in BRIGHTER_TTL_SOURCE BRIGHTER_TTL_RESULTS BRIGHTER_TTL_SCOPE BRIGHTER_TTL_UID BRIGHTER_TTL_GID BRIGHTER_TTL_MONGO_IMAGE; do
        printf 'export %s=%q\n' "$name" "${!name}"
    done
    printf '%q ' "${compose[@]}" down --volumes --remove-orphans
    printf '\n'
} > "$results/cleanup.sh"
chmod +x "$results/cleanup.sh"

finish() {
    local code=$?
    trap - EXIT
    set +e
    "${compose[@]}" logs --no-color mongo mongo-transactions > "$results/mongodb.log" 2>&1
    "${compose[@]}" ps --all > "$results/containers.txt" 2>&1
    if [[ "$keep" == 0 ]]; then
        if ! bash "$results/cleanup.sh" > "$results/cleanup.log" 2>&1; then
            printf 'Cleanup failed; run %s manually.\n' "$results/cleanup.sh" >&2
            if [[ "$code" == 0 ]]; then code=1; fi
        fi
    else
        printf 'Databases retained. Cleanup: bash %q\n' "$results/cleanup.sh"
    fi
    printf '%s\n' "$code" > "$results/exit-code.txt"
    printf '\nResults: %s\nExit code: %s\n' "$results" "$code"
    exit "$code"
}
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

printf 'Project: %s\nResults: %s\n' "$project_name" "$results"
"${compose[@]}" config > "$results/compose.resolved.yaml"
"${compose[@]}" build tests 2>&1 | tee "$results/image-build.log"
"${compose[@]}" up --detach --wait --wait-timeout 180 mongo mongo-transactions \
    2>&1 | tee "$results/startup.log"
"${compose[@]}" images > "$results/images.txt"
"${compose[@]}" run --rm --no-deps -T tests 2>&1 | tee "$results/run.log"
