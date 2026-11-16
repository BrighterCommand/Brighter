#!/usr/bin/env bash
set -euo pipefail

project=tests/Paramore.Brighter.MongoDb.Tests/Paramore.Brighter.MongoDb.Tests.csproj
scope=${BRIGHTER_TTL_SCOPE:-full}
case "$scope" in
    full|ttl) ;;
    *) printf 'Invalid test scope: %s\n' "$scope" >&2; exit 2 ;;
esac

printf 'Copying working tree, including uncommitted changes, into the container.\n'
work_dir=$(mktemp -d /work/checkout.XXXXXX)
tar -C /source --exclude=.git --exclude=bin --exclude=obj \
    --exclude=artifacts --exclude=TestResults --exclude=.idea --exclude=.vs \
    --exclude=.codex --exclude=.aws -cf - . | tar -C "$work_dir" -xf -
cd "$work_dir"

dotnet --info | tee /results/dotnet-info.log
dotnet restore "$project" --disable-parallel -p:NuGetAudit=false \
    2>&1 | tee /results/restore.log
dotnet build src/Paramore.Brighter.MongoDb/Paramore.Brighter.MongoDb.csproj \
    --configuration Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false \
    2>&1 | tee /results/library-build.log

status=0
printf 'Framework\tSuite\tResult\n' > /results/summary.tsv
for framework in net9.0 net10.0; do
    suites=(ttl)
    if [[ "$scope" == full ]]; then
        suites+=(full)
    fi
    for suite in "${suites[@]}"; do
        result_dir="/results/$framework/$suite"
        mkdir -p "$result_dir"
        args=(test "$project" --framework "$framework" --configuration Release
            --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
            --results-directory "$result_dir"
            --logger "trx;LogFileName=results.trx"
            --logger "console;verbosity=normal"
            --blame-hang-timeout 5m --blame-hang-dump-type mini)
        if [[ "$suite" == ttl ]]; then
            args+=(--filter FullyQualifiedName~MongoDbCollectionTtlIndexTests)
        fi
        printf '\nRunning %s tests on %s\n' "$suite" "$framework"
        if dotnet "${args[@]}" 2>&1 | tee "$result_dir/test.log"; then
            printf '%s\t%s\tPASS\n' "$framework" "$suite" >> /results/summary.tsv
        else
            printf '%s\t%s\tFAIL\n' "$framework" "$suite" >> /results/summary.tsv
            status=1
        fi
    done
done

cat /results/summary.tsv
exit "$status"
