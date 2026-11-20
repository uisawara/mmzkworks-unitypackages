#!/usr/bin/env bash
# release-please が生成した CHANGELOG(またはリリースノート)を整える。引数のファイルをその場で書き換える。
#
#   tidy-changelog.sh -s <scope> <file>...
#
#   - パッケージごとのファイルなので、行頭が "* " の行で、scope 表示が <scope>(例: mudashboard)を含むときだけ、
#     そのラベルごと消す("* **mudashboard:** foo" → "* foo"、"* **mudashboard, muhierarchy:** foo" → "* foo")。
#     ほかのパッケージだけのラベルや、本文の "**Note:**" などは消さない。-s を省略したときは scope 表示を消さない。
#     消すと本文が Markdown の記法("# "、"> "、"2. "、"<div>" など)で始まってしまう行は、表示が変わるので消さない。
#     消した後にも同じ形のラベルが残るなら、それは本文なので、次の実行で消さないよう ":" を同じ表示の "&#58;" にする。
#   - 空行をはさまずに並んだ 1 行だけの項目("* " で始まる行)の並びの中で、末尾の参照リンク
#     (PR の "([#12](...))" と commit の "([abc1234](...))")を除いた内容が同じ行を、最初の位置の 1 行にまとめ、
#     参照リンクを ", " 区切りの 1 つの括弧に並べる(まとめる行がなくても、この書き方にそろえる)。
#     本文の普通のリンクは外さない。
#     例: "* foo ([#12](u1)) ([abc1234](u2))" と "* foo ([def5678](u3))"
#         → "* foo ([#12](u1), [abc1234](u2), [def5678](u3))"
#   - 続きの行・入れ子・コードブロック・HTML を含む並び、直後に字下げされたまとまりが続く並びは、
#     並びも中身もそのままにする(scope 表示の削除だけ行う)。項目の続きを別の項目に付け替えないため。
#   - コードブロック("```" / "~~~")と HTML ブロックの中は書き換えない。
#     コードブロックの閉じ方が開き方と合わないなど、構造を判定しきれない入力に出会ったら、そこから後ろは書き換えない。
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
    # 項目の本文の先頭にあると、Markdown のブロックの記法として解釈される文字列か
    function starts_block_syntax(body) {
      return body ~ /^([-+*]([ \t]|$)|[ \t>#<=|]|~~~|```|\*\*\*|___|---|[0-9]+[.)]([ \t]|$)|\[[ xX]\])/ || body == ""
    }
    function strip_scope(s,   len, stripped) {
      len = scope_label_length(s)
      if (len == 0) return s
      stripped = "* " substr(s, len + 1)
      if (starts_block_syntax(substr(stripped, 3))) return s
      len = scope_label_length(stripped)
      if (len > 0) stripped = "* **" substr(stripped, 5, len - 8) "&#58;** " substr(stripped, len + 1)
      return stripped
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
    function indent_of(s) {
      match(s, /^ */)
      return RLENGTH
    }
    # コードブロックの開始行なら、その記号("```" や "~~~~")を返す(字下げは 3 スペースまで)
    function fence_open(s,   marker) {
      if (!match(s, /^(   |  | )?(```+|~~~+)/)) return ""
      marker = substr(s, 1, RLENGTH)
      sub(/^ */, "", marker)
      # "```" の後ろに "`" がある行は、インラインのコードでありコードブロックではない
      if (substr(marker, 1, 1) == "`" && index(substr(s, RLENGTH + 1), "`") > 0) return ""
      return marker
    }
    # 開いているコードブロックの閉じる記号だけの行か(同じ記号で、同じ長さ以上)
    function is_fence_close(s,   t) {
      t = s
      sub(/^[ \t]*/, "", t)
      sub(/[ \t]*$/, "", t)
      return t ~ /^(`+|~+)$/ && substr(t, 1, 1) == substr(fence, 1, 1) && length(t) >= length(fence)
    }
    # HTML ブロックの開始行か。2: 空行をまたいで続く種類(<pre> や <!-- など)、1: 次の空行まで続く種類、0: どちらでもない
    function html_kind(s,   t) {
      t = tolower(s)
      if (t ~ /^(   |  | )?<(pre|script|style|textarea)([ \t>]|$)/) return 2
      if (t ~ /^(   |  | )?<(!--|\?|![a-z]|!\[cdata\[)/) return 2
      if (t ~ /^(   |  | )?<\/?[a-z]/) return 1
      return 0
    }
    # 見出しはリストを終わらせるだけで、項目の続きにはならない
    function is_heading(s) {
      return s ~ /^(   |  | )?#(#?#?#?#?#?)([ \t]|$)/
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
    # 構造を判定しきれない入力に出会ったら、ここから後ろは書き換えない
    function freeze() {
      flush_block(1)
      print_blanks()
      frozen = 1
    }
    {
      raw = $0
      eol = (sub(/\r$/, "", raw)) ? "\r" : ""
    }
    frozen { print; next }
    # コードブロックの中は何も変えない
    fence != "" {
      print
      if (raw ~ /^[ \t]*$/) next
      ind = indent_of(raw)
      if (is_fence_close(raw) && ind <= fence_indent + 3) {
        # 開いたときと違う字下げで閉じた場合は、どこで閉じたかの解釈がずれうるので、後ろは書き換えない
        if (ind != fence_indent) frozen = 1
        fence = ""
      } else if (ind < fence_indent) {
        # 項目の中のコードブロックより浅い行が来た(項目が終わった可能性がある)
        frozen = 1
      }
      next
    }
    # 次の HTML ブロックの区切り(空行)まで、何も変えない
    in_html {
      print
      if (raw ~ /^[ \t]*$/) in_html = 0
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
      fence_indent = indent_of(raw)
      next
    }
    # "* ```" のように項目がコードブロックで始まる行は、閉じる位置の解釈がずれうるので、後ろは書き換えない
    raw ~ /^\* +(```|~~~)/ {
      freeze()
      print
      next
    }
    (kind = html_kind(raw)) > 0 {
      if (kind == 2) {
        freeze()
      } else {
        flush_block(1)
        in_html = 1
      }
      print
      next
    }
    is_heading(raw) {
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
