#!/usr/bin/env bash
# Usage: resolve-release-version.sh <major.minor.patch> <Dev|Stable> <manifest.json> <packages.lock.json> <tags-file>
# Prints version, tag and channel as key=value lines for GITHUB_OUTPUT. The tags file lists one tag per line.
# The tag is valid semver after the v: v1.2.3[-dev.N]+abstr.a.b.c[.alpha.N]. The version keeps a fourth part instead
# of -dev.N, because the assembly version can only hold numbers.
set -euo pipefail

requested=$1 channel=$2 manifest=$3 lock=$4 tags=$5

# On stderr, because stdout goes to GITHUB_OUTPUT.
refuse() {
    echo "::error::$*" >&2
    exit 1
}

# Leading zeros are refused because the build parses them away, so 6.04.0 would slip past the manifest check as 6.4.0.
part='(0|[1-9][0-9]*)'
[[ "$requested" =~ ^$part(\.$part){2}$ ]] || refuse "$requested is not major.minor.patch"
[[ "$channel" == Dev || "$channel" == Stable ]] || refuse "$channel is not Dev or Stable"

# Tags count too: a release whose manifest push failed still holds its revision. Older tags are v1.2.3[.N]-abstr-a.b.c.
known="$({ jq -r '.releases[].version' "$manifest"; sed -E 's/^v//; s/\+.*//; s/-dev\./\./; s/-.*//' "$tags"; } | grep -E "^${requested//./\\.}(\.[0-9]+)?$" | sort -u || true)"

# Shoko sorts 6.5.0 below 6.5.0.3 despite what its comparer documents, so a version's revisions and the version
# itself can't both exist: whichever came later might never be offered as an upgrade.
if [[ "$channel" == Stable ]]; then
    [[ -z "$known" ]] || refuse "Stable needs an unused version, but $requested already has:" $known
    version=$requested
    semver=$requested
else
    grep -qxF "$requested" <<< "$known" && refuse "$requested exists without a revision, so Dev has to move to a higher version"
    highest="$(sed -E 's/.*\.//' <<< "$known" | sort -n | tail -n 1)"
    revision=$((${highest:-0} + 1))
    version="$requested.$revision"
    semver="$requested-dev.$revision"
fi

abstraction="$(jq -er 'first(.dependencies[]["Shoko.Abstractions"].resolved // empty)' "$lock")" || refuse "$lock does not resolve Shoko.Abstractions"

echo "version=$version"
echo "tag=v$semver+abstr.${abstraction//-/.}"
echo "channel=$channel"
