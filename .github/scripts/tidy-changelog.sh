#!/usr/bin/env bash
# release-please が生成した CHANGELOG(またはリリースノート)を整える。引数のファイルをその場で書き換える。
#   - パッケージごとのファイルなので、各行頭の scope 表示("* **mudashboard:** ")を消す
#   - 箇条書きのまとまりごとに、末尾のリンク(PR の "([#12](...))" と commit の "([abc1234](...))")を除いた
#     内容が同じ行を 1 行にまとめ、リンクを ", " 区切りで並べる
#     例: "* foo ([#12](u1)) ([abc1234](u2))" と "* foo ([def5678](u3))"
#         → "* foo ([#12](u1), [abc1234](u2), [def5678](u3))"
# 何度実行しても結果は同じになる。
set -euo pipefail

for file in "$@"; do
  [ -f "$file" ] || continue
  tmp="$(mktemp)"
  awk '
    # 溜めている箇条書きを、最初に出てきた順に出力する
    function flush(   i) {
      for (i = 1; i <= count; i++) {
        if (refs[i] == "") print texts[i] eols[i]
        else print texts[i] " (" refs[i] ")" eols[i]
      }
      count = 0
      split("", index_of)
    }
    # refs[i] に、まだ含まれていないリンクだけを追加する
    function add_ref(i, ref,   n, parts, k) {
      n = split(refs[i], parts, ", ")
      for (k = 1; k <= n; k++) if (parts[k] == ref) return
      refs[i] = (refs[i] == "") ? ref : refs[i] ", " ref
    }
    /^\* / {
      line = $0
      # CRLF のファイルでも行末の改行コードを保つ
      eol = sub(/\r$/, "", line) ? "\r" : ""
      sub(/^\* \*\*[^*]+:\*\* /, "* ", line)
      # 末尾の "([label](url))" または "([label](url), [label](url), ...)" を順に外す
      found = ""
      while (match(line, / \((\[[^]]*\]\([^)]*\)(, )?)+\)$/)) {
        group = substr(line, RSTART + 2, RLENGTH - 3)
        found = (found == "") ? group : group ", " found
        line = substr(line, 1, RSTART - 1)
      }
      if (line in index_of) {
        i = index_of[line]
      } else {
        i = ++count
        index_of[line] = i
        texts[i] = line
        refs[i] = ""
        eols[i] = eol
      }
      n = split(found, parts, ", ")
      for (k = 1; k <= n; k++) add_ref(i, parts[k])
      next
    }
    { flush(); print }
    END { flush() }
  ' "$file" >"$tmp"
  cat "$tmp" >"$file"
  rm -f "$tmp"
done
