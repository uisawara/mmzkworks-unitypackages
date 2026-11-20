#!/usr/bin/env bash
# release-please が生成した CHANGELOG(またはリリースノート)を整える。引数のファイルをその場で書き換える。
#   - パッケージごとのファイルなので、各行頭の scope 表示("* **mudashboard:** ")を消す
#   - 見出しの区切りごとに、commit へのリンクを除いた本文が同じ行を 1 つにまとめる
#     (同じ変更が個々の commit と merge commit の両方から拾われた場合の重複を消す)
# 何度実行しても結果は同じになる。
set -euo pipefail

for file in "$@"; do
  [ -f "$file" ] || continue
  tmp="$(mktemp)"
  awk '
    /^#/ { split("", seen) }
    /^\* / {
      line = $0
      sub(/^\* \*\*[^*]+:\*\* /, "* ", line)
      key = line
      sub(/ \(\[[0-9a-f]+\]\([^)]*\)\)$/, "", key)
      if (key in seen) next
      seen[key] = 1
      print line
      next
    }
    { print }
  ' "$file" >"$tmp"
  cat "$tmp" >"$file"
  rm -f "$tmp"
done
