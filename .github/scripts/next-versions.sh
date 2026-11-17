#!/usr/bin/env bash
# 前回タグ以降のコミットから、各パッケージの次のバージョンを決める。
#
#   bash .github/scripts/next-versions.sh           # 一覧を表示するだけ
#   bash .github/scripts/next-versions.sh --apply   # package.json の version を書き換える
#
# 判定（パッケージ配下を変更した、merge 以外のコミットの subject / body を見る）:
#   破壊的変更 (type! または BREAKING CHANGE) -> major（0.x の間は minor）
#   feat                                     -> minor
#   fix / perf / refactor                    -> patch
#   それ以外 (docs / chore / test / ci など)  -> 上げない
# package.json の version が前回タグより既に進んでいる場合は、その値を尊重して触らない。
# タグが 1 つも無いパッケージは、今の version を初回リリースとして扱う。
set -euo pipefail

APPLY=0
[ "${1:-}" = "--apply" ] && APPLY=1

printf '%s\t%s\t%s\t%s\n' "package" "current" "next" "reason"

for manifest in Assets/UnityPackages/*/package.json; do
  dir="$(dirname "$manifest")"
  name="$(jq -r '.name' "$manifest")"
  current="$(jq -r '.version' "$manifest")"
  prev_tag="$(git tag -l "$name/*" --sort=-v:refname | head -n1)"

  if [ -z "$prev_tag" ]; then
    printf '%s\t%s\t%s\t%s\n' "$name" "$current" "$current" "initial release (no tag yet)"
    continue
  fi

  prev="${prev_tag#"$name/"}"
  if [ "$current" != "$prev" ]; then
    printf '%s\t%s\t%s\t%s\n' "$name" "$current" "$current" "already bumped from $prev"
    continue
  fi

  level=0 # 0 none, 1 patch, 2 minor, 3 major
  while IFS= read -r -d $'\x1e' entry; do
    subject="${entry%%$'\x1f'*}"
    body="${entry#*$'\x1f'}"
    l=0
    if [[ "$subject" =~ ^[a-z]+(\([^\)]*\))?!: ]] || [[ "$body" == *"BREAKING CHANGE"* ]]; then
      l=3
    elif [[ "$subject" =~ ^feat(\([^\)]*\))?: ]]; then
      l=2
    elif [[ "$subject" =~ ^(fix|perf|refactor)(\([^\)]*\))?: ]]; then
      l=1
    fi
    [ "$l" -gt "$level" ] && level=$l
  done < <(git log --no-merges --format='%s%x1f%b%x1e' "$prev_tag..HEAD" -- "$dir")

  if [ "$level" -eq 0 ]; then
    printf '%s\t%s\t%s\t%s\n' "$name" "$current" "-" "no releasable changes"
    continue
  fi

  IFS=. read -r major minor patch <<<"${current%%-*}"
  if [ "$level" -eq 3 ] && [ "$major" -eq 0 ]; then level=2; fi
  case "$level" in
    3) next="$((major + 1)).0.0"; reason="breaking change" ;;
    2) next="$major.$((minor + 1)).0"; reason="feat" ;;
    1) next="$major.$minor.$((patch + 1))"; reason="fix/perf/refactor" ;;
  esac

  printf '%s\t%s\t%s\t%s\n' "$name" "$current" "$next" "$reason since $prev_tag"
  if [ "$APPLY" -eq 1 ]; then
    # 行の書式・改行コードを保ったまま version の値だけ置き換える
    sed -i -E "0,/\"version\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/s//\"version\": \"$next\"/" "$manifest"
  fi
done
