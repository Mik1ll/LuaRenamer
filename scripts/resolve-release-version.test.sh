#!/usr/bin/env bash
set -uo pipefail

resolve="$(dirname "$0")/resolve-release-version.sh"
real_lock="$(dirname "$0")/../LuaRenamer/packages.lock.json"
dir="$(mktemp -d)"
trap 'rm -rf "$dir"' EXIT
echo '{"releases": [{"version": "6.4.0"}, {"version": "6.5.0.1"}, {"version": "6.5.0.3"}, {"version": "6.6.0"}, {"version": "5.10.3"}]}' > "$dir/manifest.json"
echo '{"dependencies": {"net10.0": {"Shoko.Abstractions": {"resolved": "6.0.0-alpha.108"}}, "net10.0/win-x64": {}}}' > "$dir/lock.json"
echo '{"dependencies": {"net10.0": {"Shoko.Abstractions": {"resolved": "6.0.0"}}}}' > "$dir/final-lock.json"
echo '{"dependencies": {"net10.0": {}}}' > "$dir/no-abstractions-lock.json"
printf '%s\n' v6.4.0-abstr-6.0.0-alpha.108 v6.7.0.2-abstr-6.0.0-alpha.108 v6.8.0-abstr-6.0.0 v5.10.3-stable-5.3.1 \
    v6.9.0-dev.4+abstr.6.0.0.alpha.108 v6.10.0+abstr.6.0.0 > "$dir/tags"

failures=0
run() {
    bash "$resolve" "$1" "$2" "$dir/manifest.json" "${3:-$dir/lock.json}" "$dir/tags" 2> /dev/null
}
expect() {
    local expected_tag=$1 version=$2 channel=$3 lock=${4:-} actual
    actual="$(run "$version" "$channel" "$lock" | sed -n 's/^tag=//p')"
    if [[ "$actual" != "$expected_tag" ]]; then
        echo "$version ($channel): expected $expected_tag, got ${actual:-a refusal}"
        failures=$((failures + 1))
    fi
}
refuse() {
    local version=$1 channel=$2 lock=${3:-}
    if run "$version" "$channel" "$lock" > /dev/null; then
        echo "$version ($channel): expected a refusal"
        failures=$((failures + 1))
    fi
}

expect v7.0.0-dev.1+abstr.6.0.0.alpha.108 7.0.0 Dev
expect v7.0.0-dev.1+abstr.6.0.0 7.0.0 Dev "$dir/final-lock.json"
expect v6.5.0-dev.4+abstr.6.0.0.alpha.108 6.5.0 Dev
expect v6.7.0-dev.3+abstr.6.0.0.alpha.108 6.7.0 Dev
expect v6.9.0-dev.5+abstr.6.0.0.alpha.108 6.9.0 Dev
expect v7.0.0+abstr.6.0.0 7.0.0 Stable "$dir/final-lock.json"
expect v0.1.0+abstr.6.0.0.alpha.108 0.1.0 Stable
# Stable on a prerelease Abstractions version is the maintainer's call.
expect v7.0.0+abstr.6.0.0.alpha.108 7.0.0 Stable

if [[ "$(run 6.5.0 Dev | sed -n 's/^version=//p')" != 6.5.0.4 ]]; then
    echo "A Dev version has to keep its revision as a fourth part"
    failures=$((failures + 1))
fi

refuse 6.4.0 Dev
refuse 6.6.0 Dev
refuse 6.8.0 Dev
refuse 6.10.0 Dev
refuse 6.5.0 Stable
refuse 6.7.0 Stable
refuse 6.9.0 Stable
refuse 6.6.0 Stable
refuse 6.8.0 Stable
refuse 6.10.0 Stable

refuse 7.0 Dev
refuse 7.0.0.1 Dev
refuse 7 Stable
refuse v7.0.0 Dev
refuse 07.0.0 Dev
refuse 7.00.0 Dev
refuse 7.0.00 Stable
refuse 7.0.0rc1 Stable
refuse 7.0.0-1 Dev
refuse '' Dev
refuse 7.0.0 Debug
refuse 7.0.0 Dev "$dir/no-abstractions-lock.json"

# The real lock file, so a change in its layout fails here rather than during a release.
run 7.0.0 Dev "$real_lock" | grep -qE '^tag=v7\.0\.0-dev\.1\+abstr\.[0-9]' || {
    echo "The real lock file did not resolve an Abstractions version"
    failures=$((failures + 1))
}

((failures == 0)) && echo "All release version checks passed"
exit "$((failures > 0))"
