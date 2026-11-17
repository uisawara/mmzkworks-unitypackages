---
name: review-develop
description: 最新の develop を Claude がローカルでレビューし、指摘を修正して develop 向けの PR を作る。既定ではまだ main に入っていない変更（origin/main...origin/develop）すべてが対象。「develop をレビューして」「develop をレビューして直して PR にして」と言われたとき、または /review-develop で使う。引数でレビューの起点（コミット・タグ・ブランチ）やレベルを指定できる。
---

# develop をレビューして直し、PR にする

CLAUDE.md のブランチ・コミット・Unity パッケージの約束に従う。ユーザーの作業ツリー（Unity で開いているチェックアウト）には触れず、作業はすべて worktree で行う。マージはしない。

## 1. 対象を決める

```bash
git fetch origin --tags --prune
```

- 既定の対象は、まだリリースされていない変更すべて: `origin/main...origin/develop`。
- 引数で起点が指定されたら（例: `/review-develop 8c8297b`、`/review-develop works.mmzk.muhierarchy/0.3.1`）、`<起点>...origin/develop` を対象にする。
- 引数でレベルが指定されたら（`low` / `medium` / `high` / `xhigh`）それを使い、なければ `high`。`max` / `ultra` はユーザーが明示したときだけ。
- 対象のコミットと変更ファイルの一覧を出す。差分がなければ、レビューするものがないと伝えて終える。

```bash
git log --oneline --no-merges <base>..origin/develop
git diff --stat <base>...origin/develop
```

## 2. 作業ツリーを作る

スクラッチ領域に `git worktree add -b fix/review-develop-<YYYYMMDD> <path> origin/develop` で作業ツリーを作り、以降はそこで作業する。

## 3. レビューして直す

1. `code-review` スキルを `<base>...origin/develop` の差分を対象に、手順 1 で決めたレベルと `--fix` で実行する。作業ツリーは手順 2 のものを使う。
2. 修正をパッケージごとに確認し、`fix(<scope>): develop レビューの指摘を修正する` のようにコミットする（Conventional Commits。scope はパッケージの短縮名。テストだけの修正は `test(<scope>)`、ドキュメントだけなら `docs(<scope>)`）。
3. 直さなかった指摘は、理由（誤検知、仕様どおり、影響が小さく別対応にする、など）を控える。
4. 確認できる範囲で検証する。
   - `package.json` / asmdef が JSON として正しいか（`jq . <file>`）。
   - 新規ファイル・フォルダに `.meta` があるか（`~` で終わるフォルダは不要）。
   - README を変えたら英語版と日本語版の両方が更新されているか。
   - 新しい Unity API が `#if UNITY_6000_x_OR_NEWER` などで囲まれ、2022.3 でもコンパイルできる書き方か。
   - Unity でのコンパイル・テストは worktree では実行できない。確認できなかったことは PR 本文のチェックリストに未チェックで残す。

## 4. PR を作る

- 修正がなければ PR は作らない。レビュー結果（指摘と、直さなかった理由）だけを報告して、worktree を片付けて終える。
- 修正があれば push して `gh pr create --base develop` で PR を作る。タイトルは `fix: develop レビューの指摘を修正する (<YYYY-MM-DD>)`。本文:

```markdown
## 概要
develop（<base>...origin/develop、<n> コミット）を Claude がローカルでレビューし、指摘を修正しました。

## レビュー
- 対象: <base>...<develop の短い SHA>
- code-review（<レベル>）: 指摘 <n> 件、うち修正 <m> 件

## 修正したもの
- <パッケージ>: <内容>（<ファイル:行>）

## 修正しなかった指摘
- <内容> — <理由>

## 確認
- [ ] Unity 6000.x でコンパイル・テスト
- [ ] Unity 2022.3 でコンパイル

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

- `version` は上げない（リリース準備で `/release` が上げる）。
- `git worktree remove` で作業ツリーを片付ける。

## 5. 報告する

PR の URL（作った場合）、修正したもの・しなかったものとその理由、未確認の項目を伝える。マージはユーザーの指示を待つ。
