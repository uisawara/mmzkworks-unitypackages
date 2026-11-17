---
name: pr
description: このリポジトリで作業内容を develop 向けの PR にする手順。ローカルで Claude のコードレビューと修正を行ってから、規約どおりのコミット・PR を作る。「PR を作って」「PR にして」と言われたとき、または /pr で使う。
---

# PR を作る

CLAUDE.md のブランチ・コミット・Unity パッケージの約束に従う。マージはしない。

## 1. 作業ブランチを用意する

- 変更がすでに `feature/*` / `fix/*` ブランチにあればそれを使う。
- 変更がユーザーの作業ツリー（Unity で開いているチェックアウト）に未コミットで散らばっている場合は、PR に含めるファイルをユーザーと確認する。テスト用アセットなど無関係な変更は含めない。
- 新しいブランチが要るときは、スクラッチ領域に `git worktree add -b <feature|fix>/<topic> <path> origin/develop` で作業ツリーを作り、そこに対象の変更だけを移す（`git diff HEAD -- <paths>` をパッチにして `git apply --index`。新規ファイルは `git add -N` してから diff に含める）。ユーザーの作業ツリーは書き換えない。

## 2. ローカルでレビューして直す

1. `code-review` スキルを、`origin/develop` との差分を対象に `--fix` 付きで実行する（レベルはユーザーの指定がなければ `high`）。
2. 修正が入ったら、変更内容を確認する。直さなかった指摘は理由を控えておき、PR 本文に書く。
3. 確認できる範囲で検証する。
   - `package.json` / asmdef が JSON として正しいか（`jq . <file>`）。
   - `Assets/` 配下の新規ファイル・フォルダに `.meta` があるか。
   - README を変えたら英語版と日本語版の両方が更新されているか。
   - C# を変えたら、Unity が開いていれば `unity command console status` でコンパイルエラーがないこと、可能なら `unity command test run` でテストを確認する。確認できなかったことは PR 本文のチェックリストに未チェックで残す。

## 3. コミットして PR を作る

- Conventional Commits（`<type>(<scope>): <日本語の要約>`）でコミットする。type はバージョン判定に使われるので、機能追加は `feat`、不具合修正は `fix` を正しく付ける。レビューでの修正は、元の変更と同じコミットにまとめるか、`fix(<scope>): レビュー指摘を修正する` として分ける。
- `package.json` の version は原則上げない（リリース準備で `/release` が上げる）。
- push して `gh pr create --base develop` で PR を作る。本文は次の形:

```markdown
## 概要
<何をなぜ変えたか>

## 変更点
- <パッケージごとの主な変更>

## ローカルレビュー
- code-review（<レベル>）: 指摘 <n> 件、うち修正 <m> 件
- 修正しなかった指摘: <なし / 理由つきで列挙>

## 確認
- [ ] Unity 6000.x でコンパイル・テスト
- [ ] Unity 2022.3 でコンパイル

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

- 作業用の worktree を使った場合は `git worktree remove` で片付ける。

## 4. 報告する

PR の URL、レビューで直したこと・直さなかったこと、未確認の項目を伝える。マージはユーザーの指示を待つ。
