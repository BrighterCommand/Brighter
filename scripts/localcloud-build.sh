#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
root=$(pwd)
phase_temp=$(mktemp -d /tmp/brighter-localcloud.XXXXXX)
trap 'rm -rf "$phase_temp"' EXIT
export TMPDIR="$phase_temp"
export DOTNET_CLI_HOME="$root/obj/localcloud/cli-home"
export NUGET_PACKAGES="$root/obj/localcloud/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
export MSBUILDDISABLENODEREUSE=1

sdk="$root/obj/localcloud/dotnet"
if command -v dotnet >/dev/null &&
    [[ "$(dotnet --version)" == 10.* ]] &&
    dotnet --list-sdks | grep -q '^9\.' &&
    dotnet --list-sdks | grep -q '^10\.'; then
    dotnet_command=$(command -v dotnet)
else
    for channel in 9.0 10.0; do
        if [[ ! -x "$sdk/dotnet" ]] || ! "$sdk/dotnet" --list-sdks | grep -q "^$channel\."; then
            bash dotnet-install.sh --channel "$channel" --install-dir "$sdk" --no-path
        fi
    done
    dotnet_command="$sdk/dotnet"
fi
export PATH="$(dirname "$dotnet_command"):$PATH"

if [[ "${1:-}" == --dotnet ]]; then
    shift
    "$dotnet_command" "$@"
    exit
fi

if ! command -v timeout >/dev/null; then
    mkdir -p "$root/obj/localcloud/bin"
    if command -v gtimeout >/dev/null; then
        ln -sf "$(command -v gtimeout)" "$root/obj/localcloud/bin/timeout"
    elif [[ ! -x "$root/obj/localcloud/bin/timeout" ]]; then
        curl --fail --location --silent --show-error https://ftp.gnu.org/gnu/coreutils/coreutils-9.7.tar.xz \
            -o "$root/obj/localcloud/coreutils.tar.xz"
        tar -xf "$root/obj/localcloud/coreutils.tar.xz" -C "$root/obj/localcloud"
        (
            cd "$root/obj/localcloud/coreutils-9.7"
            if [[ -x /Library/Developer/CommandLineTools/usr/bin/clang ]]; then
                export CC=/Library/Developer/CommandLineTools/usr/bin/clang
                export SDKROOT=/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk
            fi
            env -u CPPFLAGS -u LDFLAGS -u CFLAGS ./configure --disable-nls --disable-gcc-warnings > configure.log 2>&1
            env -u CPPFLAGS -u LDFLAGS -u CFLAGS make -j4 > make.log 2>&1
        )
        cp "$root/obj/localcloud/coreutils-9.7/src/timeout" "$root/obj/localcloud/bin/timeout"
    fi
    export PATH="$root/obj/localcloud/bin:$PATH"
fi

bash test_check_solution_projects.sh
bash check_solution_projects.sh
bash test_clean_failed_tests_aws_assets_offline.sh
"$dotnet_command" --list-sdks
"$dotnet_command" restore --verbosity quiet
"$dotnet_command" build --configuration Release --no-restore --verbosity quiet
