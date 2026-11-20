#!/usr/bin/env bash
# release-please が生成した CHANGELOG(またはリリースノート)を整える。引数のファイルをその場で書き換える。
#
#   tidy-changelog.sh -s <scope> <file>...
#
#   - パッケージごとのファイルなので、行頭が "* " の行で、scope 表示が <scope>(例: mudashboard)を含むときだけ、
#     そのラベルごと消す("* **mudashboard:** foo" → "* foo"、"* **mudashboard, muhierarchy:** foo" → "* foo")。
#     ほかのパッケージだけのラベルや、本文の "**Note:**" などは消さない。-s を省略したときは scope 表示を消さない。
#     消した後にも同じ形のラベルが残るなら、それは本文なので、次の実行で消さないよう ":" を同じ表示の "&#58;" にする。
#   - 1 行だけの項目("* " で始まる行)だけでできた箇条書きの中で、末尾の参照リンク
#     (PR の "([#12](...))" と commit の "([abc1234](...))")を除いた内容が同じ行を、最初の位置の 1 行にまとめ、
#     参照リンクを ", " 区切りで並べる。本文の普通のリンクは外さない。
#     例: "* foo ([#12](u1)) ([abc1234](u2))" と "* foo ([def5678](u3))"
#         → "* foo ([#12](u1), [abc1234](u2), [def5678](u3))"
#   - 続きの行・入れ子・コードブロックなど、1 行で終わらない項目を含む箇条書きは、並びも中身もそのままにする
#     (scope 表示の削除だけ行う)。項目の続きを別の項目に付け替えてしまわないため。
#   - コードブロック("```" / "~~~")の中は書き換えない。判定に迷う行は書き換えない側に倒す。
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

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

for file in "$@"; do
  [ -f "$file" ] || continue
  awk -v scope="$scope" '
    # 行頭が "* **a, b:** " で、ラベルが scope を含むなら、その長さを返す(含まなければ 0)
    function scope_label_length(s,   len, label, names, n, k) {
      if (scope == "" || !match(s, /^\* \*\*[^*]+:\*\* /)) return 0
      len = RLENGTH
      label = substr(s, 5, len - 8)
      n = split(label, names, /, */)
      for (k = 1; k <= n; k++) if (tolower(names[k]) == tolower(scope)) return len
      return 0
    }
    function strip_scope(s,   len) {
      len = scope_label_length(s)
      if (len == 0) return s
      s = "* " substr(s, len + 1)
      len = scope_label_length(s)
      if (len > 0) s = "* **" substr(s, 5, len - 8) "&#58;** " substr(s, len + 1)
      return s
    }
    # release-please が付ける参照リンク(PR 番号か commit の SHA)かどうか
    function is_ref(link,   label) {
      if (!match(link, /^\[[^]]*\]\([^)]*\)$/)) return 0
      label = substr(link, 2, index(link, "]") - 2)
      if (label ~ /^#[0-9]+$/) return 1
      return label ~ /^[0-9a-f]+$/ && length(label) >= 7 && length(label) <= 40
    }
    # "[a](u), [b](u)" がすべて参照リンクなら 1 を返す
    function all_refs(group,   n, parts, k) {
      n = split(group, parts, ", ")
      for (k = 1; k <= n; k++) if (!is_ref(parts[k])) return 0
      return n > 0
    }
    # 1 行の項目か("* " で始まり、区切り線 "* * *" ではない)
    function is_entry(s) {
      return s ~ /^\* / && s !~ /^\*[ \t]*\*[ \t]*\*[* \t]*$/
    }
    # コードブロックの開始行なら、その記号("```" や "~~~~")を返す(字下げは 3 スペースまで)
    function fence_open(s,   t, marker) {
      if (!match(s, /^(   |  | )?(```+|~~~+)/)) return ""
      t = substr(s, 1, RLENGTH)
      marker = t
      sub(/^ */, "", marker)
      # "```" の後ろに "`" がある行は、インラインのコードでありコードブロックではない
      if (substr(marker, 1, 1) == "`" && index(substr(s, RLENGTH + 1), "`") > 0) return ""
      return marker
    }
    # 開いているコードブロックを閉じる行かどうか(同じ記号で、同じ長さ以上。字下げは 3 スペースまで)
    function fence_closes(s,   t) {
      if (!match(s, /^(   |  | )?(`+|~+)[ \t]*$/)) return 0
      t = s
      sub(/^ */, "", t)
      sub(/[ \t]*$/, "", t)
      return substr(t, 1, 1) == substr(fence, 1, 1) && length(t) >= length(fence)
    }
    # リストを終わらせるだけで、項目の続きにはならない行(見出し、<details> / <summary>)
    function ends_list(s) {
      return s ~ /^#(#?#?#?#?#?)([ \t]|$)/ || tolower(s) ~ /^<\/?(details|summary)([ \t>]|\/?>|$)/
    }
    # 溜めているまとまり(空行で区切られた行の並び)を出力する。
    # 1 行の項目だけでできていて、keep でなければ、同じ内容の項目をまとめる。
    function flush_block(keep,   i, j, simple, line, found, group, start, n, parts, k, count, texts, refs, eols, index_of) {
      if (bn == 0) return
      simple = !keep
      for (i = 1; i <= bn && simple; i++) if (!is_entry(braw[i])) simple = 0
      if (!simple) {
        for (i = 1; i <= bn; i++) {
          if (is_entry(braw[i])) printf "%s%s\n", strip_scope(braw[i]), beol[i]
          else printf "%s\n", borig[i]
        }
        bn = 0
        return
      }
      count = 0
      for (i = 1; i <= bn; i++) {
        line = strip_scope(braw[i])
        # 末尾の参照リンクの括弧 "([#12](u))" や "([abc1234](u), [def5678](u))" を順に外す
        found = ""
        while (match(line, / \((\[[^]]*\]\([^)]*\)(, )?)+\)$/)) {
          # all_refs の中の match で RSTART が変わるので、先に位置を取っておく
          start = RSTART
          group = substr(line, RSTART + 2, RLENGTH - 3)
          if (!all_refs(group)) break
          found = (found == "") ? group : group ", " found
          line = substr(line, 1, start - 1)
        }
        if (line in index_of) {
          j = index_of[line]
        } else {
          j = ++count
          index_of[line] = j
          texts[j] = line
          refs[j] = ""
          eols[j] = beol[i]
        }
        n = split(found, parts, ", ")
        for (k = 1; k <= n; k++) {
          if (index(", " refs[j] ", ", ", " parts[k] ", ") == 0) refs[j] = (refs[j] == "") ? parts[k] : refs[j] ", " parts[k]
        }
      }
      for (j = 1; j <= count; j++) {
        if (refs[j] == "") printf "%s%s\n", texts[j], eols[j]
        else printf "%s (%s)%s\n", texts[j], refs[j], eols[j]
      }
      bn = 0
    }
    function print_blanks() {
      printf "%s", blanks
      blanks = ""
    }
    {
      raw = $0
      eol = (sub(/\r$/, "", raw)) ? "\r" : ""
    }
    # コードブロックの中は何も変えない
    fence != "" {
      print
      if (fence_closes(raw)) fence = ""
      next
    }
    # 空行(空白だけの行も含む)は、次の行が来るまで保留する。
    # 次の行が字下げされていれば、それは直前の項目の続きなので、直前のまとまりはそのままにする。
    raw ~ /^[ \t]*$/ {
      blanks = blanks $0 "\n"
      next
    }
    {
      if (bn > 0 && blanks != "") flush_block(raw ~ /^[ \t]/)
      print_blanks()
    }
    (marker = fence_open(raw)) != "" {
      # 空行なしで続くコードブロックは直前の項目の一部なので、直前のまとまりはそのままにする
      flush_block(1)
      print
      fence = marker
      next
    }
    ends_list(raw) {
      flush_block(0)
      print
      next
    }
    {
      bn++
      braw[bn] = raw
      borig[bn] = $0
      beol[bn] = eol
    }
    END {
      flush_block(0)
      print_blanks()
    }
  ' "$file" >"$tmp"
  cat "$tmp" >"$file"
done
