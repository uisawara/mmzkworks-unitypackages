#!/usr/bin/env bash
# Assets/UnityPackages/*/package.json の version を見て、
# 未作成の "<name>/<version>" タグと GitHub Release を作成する。
# DRY_RUN=1 で実行すると、タグ・push・Release を作らずに内容だけ表示する。
set -euo pipefail

REPO_URL="https://github.com/${GITHUB_REPOSITORY:-uisawara/mmzkworks-unitypackages}.git"
DRY_RUN="${DRY_RUN:-0}"
created=0

for manifest in Assets/UnityPackages/*/package.json; do
  dir="$(dirname "$manifest")"
  name="$(jq -r '.name' "$manifest")"
  version="$(jq -r '.version' "$manifest")"
  tag="$name/$version"

  if git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
    echo "skip: $tag (already exists)"
    continue
  fi

  prev="$(git tag -l "$name/*" --sort=-v:refname | head -n1)"
  range="HEAD"
  [ -n "$prev" ] && range="$prev..HEAD"

  notes="$(mktemp)"
  {
    if [ -n "$prev" ]; then
      echo "## Changes since $prev"
    else
      echo "## Changes"
    fi
    echo
    log="$(git log --no-merges --pretty='- %s (%h)' "$range" -- "$dir" || true)"
    echo "${log:-- No changes recorded in $dir}"
    echo
    echo "## Install"
    echo
    echo '```'
    echo "$REPO_URL?path=$dir#$tag"
    echo '```'
  } >"$notes"

  echo "release: $tag (prev: ${prev:-none})"
  if [ "$DRY_RUN" = "1" ]; then
    sed 's/^/    /' "$notes"
  else
    git tag -a "$tag" -m "$name $version"
    git push origin "refs/tags/$tag"
    gh release create "$tag" --title "$name $version" --notes-file "$notes" --verify-tag
  fi
  rm -f "$notes"
  created=$((created + 1))
done

echo "done: $created release(s)"
