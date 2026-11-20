#!/usr/bin/env bash
# release-please が生成した CHANGELOG(またはリリースノート)を整える。引数のファイルをその場で書き換える。
#
#   tidy-changelog.sh -s <scope> <file>...
#
#   - パッケージごとのファイルなので、行頭の scope 表示が <scope>(例: mudashboard)を含むときだけ、
#     そのラベルごと消す("* **mudashboard:** foo" → "* foo"、"* **mudashboard, muhierarchy:** foo" → "* foo")。
#     ほかのパッケージだけのラベルや、本文の "**Note:**" などは消さない。-s を省略したときは何も消さない。
#   - 箇条書きのまとまりごとに、末尾の参照リンク(PR の "([#12](...))" と commit の "([abc1234](...))")を
#     除いた内容が同じ行を 1 行にまとめ、参照リンクを ", " 区切りで並べる。本文の普通のリンクは外さない。
#     例: "* foo ([#12](u1)) ([abc1234](u2))" と "* foo ([def5678](u3))"
#         → "* foo ([#12](u1), [abc1234](u2), [def5678](u3))"
#   - 項目の下の続きの行(字下げ行と、空行なしで続く字下げなしの行。BREAKING CHANGES の説明など)は、
#     その項目に付けたまま移動する。字下げなしの行には、表示が変わらない 2 スペースの字下げを付ける。
#     まとめた項目どうしで完全に同じまとまりは 1 回だけ残し、違うまとまりは空行をはさんで並べる。
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
    # 行頭の "**a, b:** " が scope を含むときだけ取り除く
    function strip_scope(s,   len) {
      len = scope_label_length(s)
      if (len == 0) return s
      s = "* " substr(s, len + 1)
      # 取り除いた後も同じ形のラベルが残るなら、それは本文なので、次の実行で消さないよう
      # 見た目が同じ "__a:__ " に書き換える
      len = scope_label_length(s)
      if (len > 0) s = "* __" substr(s, 5, len - 8) ":__ " substr(s, len + 1)
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
    # refs[i] に、まだ含まれていないリンクだけを追加する
    function add_ref(i, ref,   n, parts, k) {
      n = split(refs[i], parts, ", ")
      for (k = 1; k <= n; k++) if (parts[k] == ref) return
      refs[i] = (refs[i] == "") ? ref : refs[i] ", " ref
    }
    # 読み途中の項目の字下げ行のまとまりを、まとめ先の項目に付ける
    function close_item(   block, key) {
      if (cur > 0 && cur_tail != "") {
        # 先頭の空行の有無は比べない
        key = cur_tail
        sub(/^([ \t]*\r?\n)+/, "", key)
        if (!((cur, key) in seen_tail)) {
          seen_tail[cur, key] = 1
          block = cur_tail
          # 別の項目から来たまとまりは、空行をはさんで並べる(段落がつながらないように)
          if (tails[cur] != "" && block !~ /^[ \t]*\r?\n/) block = eols[cur] "\n" block
          tails[cur] = tails[cur] block
        }
      }
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
      split("", seen_tail)
    }
    function print_blanks() {
      printf "%s", blanks
      blanks = ""
    }
    {
      raw = $0
      eol = (sub(/\r$/, "", raw)) ? "\r" : ""
    }
    # 空行(空白だけの行も含む)は、次に来る行が字下げ行(項目の続き)かどうかが分かるまで保留する
    raw ~ /^[ \t]*$/ {
      blanks = blanks $0 "\n"
      next
    }
    /^\* / {
      # 空行をはさんだ箇条書きは別のまとまりとして扱う
      if (blanks != "") { flush(); print_blanks() }
      close_item()
      line = strip_scope(raw)
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
    # 項目のすぐ下(空行なし)の字下げなしの行は、Markdown では前の項目の続きになる
    # (BREAKING CHANGES の説明の 2 行目以降など)。移動しても項目の続きのままになるよう、
    # 表示が変わらない 2 スペースの字下げを付けて項目に付ける。見出し・HTML・区切り線は除く。
    cur > 0 && blanks == "" && raw !~ /^(#|<|---)/ {
      cur_tail = cur_tail "  " $0 "\n"
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
done
