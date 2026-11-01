#!/usr/bin/env bash
set -uo pipefail

check="$(dirname "$0")/check-release-version.sh"
manifest="$(mktemp)"
trap 'rm -f "$manifest"' EXIT
echo '{"releases": [{"version": "6.4.0"}, {"version": "6.4.0.1"}, {"version": "5.10.3"}]}' > "$manifest"

failures=0
expect() {
    local expected=$1 tag=$2 channel=$3 actual=pass
    bash "$check" "$tag" "$channel" "$manifest" > /dev/null || actual=fail
    if [[ "$actual" != "$expected" ]]; then
        echo "$tag ($channel): expected $expected, got $actual"
        failures=$((failures + 1))
    fi
}

expect pass v6.5.0 Stable
expect pass v6.5.0-abstr-6.0.0-alpha.108 Stable
expect pass v0.1.0 Stable
expect pass v6.4.0.2 Dev
expect pass v6.4.0.2-abstr-6.0.0-alpha.108 Dev
expect pass v6.5.0.10 Dev

expect fail v6.5.0 Dev
expect fail v6.5.0.0 Dev
expect fail v6.5.0.1 Stable
expect fail v6.4.0 Stable
expect fail v6.4.0.1-abstr-6.0.0-alpha.110 Dev
expect fail v06.5.0 Stable
expect fail v6.05.0 Stable
expect fail v6.5.00 Stable
expect fail v6.04.0-abstr-6.0.0-alpha.108 Stable
expect fail v6.5.0.01 Dev
expect fail v6.5 Stable
expect fail v6.5.0.1.2 Dev
expect fail v6.5.0rc1 Stable

((failures == 0)) && echo "All release version checks passed"
exit "$((failures > 0))"
