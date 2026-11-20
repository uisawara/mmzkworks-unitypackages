#!/usr/bin/env bash
# release-please が生成した CHANGELOG(またはリリースノート)を整える。引数のファイルをその場で書き換える。
#
#   tidy-changelog.sh -s <scope> <file>...
#
#   - パッケージごとのファイルなので、行頭の scope 表示が <scope>(例: mudashboard)を含むときだけ消す
#     ("* **mudashboard:** foo" → "* foo")。ほかの太字(本文の "**Note:**" など)は消さない。
#     -s を省略したときは scope を消さない。
#   - 箇条書きのまとまりごとに、末尾のリンク(PR の "([#12](...))" と commit の "([abc1234](...))")を除いた
#     内容が同じ行を 1 行にまとめ、リンクを ", " 区切りで並べる
#     例: "* foo ([#12](u1)) ([abc1234](u2))" と "* foo ([def5678](u3))"
#         → "* foo ([#12](u1), [abc1234](u2), [def5678](u3))"
#   - 項目の下の字下げされた行(BREAKING CHANGES の説明など)は、その項目に付けたまま移動する。
#     まとめた項目どうしで同じ内容の字下げ行は 1 回だけ残す。
# 何度実行しても結果は同じになる。CRLF のファイルでも行末の改行コードを保つ。
set -euo pipefail

scope=""
while getopts "s:" opt; do
  case "$opt" in
    s) scope="$OPTARG" ;;
    *) echo "usage: $0 [-s scope] file..." >&2; exit 2 ;;
  esac
done
shift $((OPTIND - 1))

for file in "$@"; do
  [ -f "$file" ] || continue
  tmp="$(mktemp)"
  awk -v scope="$scope" '
    # 行頭の "**a, b:** " が scope を含むときだけ取り除く
    function strip_scope(s,   label, names, n, k) {
      if (scope == "" || !match(s, /^\* \*\*[^*]+:\*\* /)) return s
      label = substr(s, 5, RLENGTH - 8)
      n = split(label, names, /, */)
      for (k = 1; k <= n; k++) {
        if (tolower(names[k]) == tolower(scope)) return "* " substr(s, RLENGTH + 1)
      }
      return s
    }
    # refs[i] に、まだ含まれていないリンクだけを追加する
    function add_ref(i, ref,   n, parts, k) {
      n = split(refs[i], parts, ", ")
      for (k = 1; k <= n; k++) if (parts[k] == ref) return
      refs[i] = (refs[i] == "") ? ref : refs[i] ", " ref
    }
    # 読み途中の項目の字下げ行を、まとめ先の項目に付ける(同じ内容がすでにあれば付けない)
    function close_item() {
      if (cur > 0 && cur_tail != "" && index(tails[cur], cur_tail) == 0) tails[cur] = tails[cur] cur_tail
      cur = 0
      cur_tail = ""
    }
    # 溜めている項目を、最初に出てきた順に出力する
    function flush(   i) {
      close_item()
      for (i = 1; i <= count; i++) {
        if (refs[i] == "") printf "%s%s\n", texts[i], eols[i]
        else printf "%s (%s)%s\n", texts[i], refs[i], eols[i]
        printf "%s", tails[i]
      }
      count = 0
      split("", index_of)
    }
    function print_blanks() {
      printf "%s", blanks
      blanks = ""
    }
    {
      raw = $0
      eol = (sub(/\r$/, "", raw)) ? "\r" : ""
    }
    # 空行は、次に来る行が字下げ行(項目の続き)かどうかが分かるまで保留する
    raw == "" {
      blanks = blanks $0 "\n"
      next
    }
    /^\* / {
      # 空行をはさんだ箇条書きは別のまとまりとして扱う
      if (blanks != "") { flush(); print_blanks() }
      close_item()
      line = strip_scope(raw)
      # 末尾の "([label](url))" または "([label](url), [label](url), ...)" を順に外す
      found = ""
      while (match(line, / \((\[[^]]*\]\([^)]*\)(, )?)+\)$/)) {
        group = substr(line, RSTART + 2, RLENGTH - 3)
        found = (found == "") ? group : group ", " found
        line = substr(line, 1, RSTART - 1)
      }
      if (line in index_of) {
        cur = index_of[line]
      } else {
        cur = ++count
        index_of[line] = cur
        texts[cur] = line
        refs[cur] = ""
        tails[cur] = ""
        eols[cur] = eol
      }
      n = split(found, parts, ", ")
      for (k = 1; k <= n; k++) add_ref(cur, parts[k])
      next
    }
    # 項目の下の字下げ行は、その項目に付ける
    /^[ \t]/ && cur > 0 {
      cur_tail = cur_tail blanks $0 "\n"
      blanks = ""
      next
    }
    # それ以外(見出し・本文など)が来たら、まとまりを閉じて出力する
    {
      flush()
      print_blanks()
      print
    }
    END {
      flush()
      print_blanks()
    }
  ' "$file" >"$tmp"
  cat "$tmp" >"$file"
  rm -f "$tmp"
done
