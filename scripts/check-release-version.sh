#!/usr/bin/env bash
# Usage: check-release-version.sh <tag> <Dev|Stable> <manifest.json>
set -euo pipefail

tag=$1 channel=$2 manifest=$3
version="${tag#v}"; version="${version%%-*}"

# Leading zeros are refused because the build parses them away, so 6.04.0 would slip past the manifest check as 6.4.0.
part='(0|[1-9][0-9]*)'
# The server reads a revision as Dev, so the revision has to agree with the channel.
if [[ "$channel" == Dev ]]; then
    pattern="^$part(\.$part){2}\.[1-9][0-9]*\$"
else
    pattern="^$part(\.$part){2}\$"
fi
[[ "$version" =~ $pattern ]] || {
    echo "::error::$version does not fit $channel: prerelease tags need a revision (v1.2.3.4), Stable tags must not have one (v1.2.3)"
    exit 1
}

# shoko-build matches releases by version alone, so reusing a version would overwrite that release.
jq -e --arg version "$version" 'any(.releases[]; .version == $version) | not' "$manifest" > /dev/null || {
    echo "::error::$version is already in the manifest"
    exit 1
}
