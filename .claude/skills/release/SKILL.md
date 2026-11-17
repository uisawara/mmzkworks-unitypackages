---
name: release
description: このリポジトリのリリース手順。develop と main の差分を Claude がローカルでレビュー・修正し、パッケージのバージョンを上げる準備 PR（develop 向け）を作り、マージ後に develop → main のリリース PR を作る。「リリースして」「リリース準備」と言われたとき、または /release で使う。
---

# リリースする

CLAUDE.md のバージョンとリリースの約束に従う。マージはユーザーの指示があるときだけ行う。main へのマージで release workflow がタグと GitHub Release を作るので、main 向け PR のマージは特に慎重に扱う。

## 1. リリース対象を確認する

```bash
git fetch origin --tags --prune
git log --oneline --no-merges origin/main..origin/develop
```

差分がなければ、リリースするものがないと伝えて終える。

## 2. 準備ブランチを作る

スクラッチ領域に `git worktree add -b release/prepare-<YYYYMMDD> <path> origin/develop` で作業ツリーを作る。以降の作業はすべてそこで行い、ユーザーの作業ツリーには触れない。

## 3. ローカルでレビューして直す

1. `code-review` スキルを、`origin/main...origin/develop` の差分（リリースに入る変更すべて）を対象に `--fix` 付きで実行する（レベルはユーザーの指定がなければ `high`）。
2. 修正は `fix(<scope>): リリース前レビューの指摘を修正する` としてパッケージごとにコミットする。直さなかった指摘は理由を控える。
3. `/pr` の「確認できる範囲で検証する」と同じ確認をする。特に、パッケージを Unity 2022.3 でもコンパイルできる書き方になっているか（新しい API が `#if UNITY_6000_x_OR_NEWER` で囲まれているか）を見る。

## 4. バージョンを上げる

```bash
bash .github/scripts/next-versions.sh           # 一覧を確認
bash .github/scripts/next-versions.sh --apply   # package.json を書き換える
```

- 一覧（package / current / next / reason）をユーザーに見せる。判定に納得できない行があれば、該当する `package.json` を手で直してよい。
- `chore(release): リリースするパッケージのバージョンを上げる` としてコミットする（上げるものがなければコミットしない）。
- `DRY_RUN=1 bash .github/scripts/release.sh` で、main に入ったときに作られるタグの一覧を得る。

## 5. 準備 PR を作る（develop 向け）

push して `gh pr create --base develop` で PR を作る。タイトルは `chore(release): リリース準備 <YYYY-MM-DD>`。本文には次を書く:

- ローカルレビューの結果（レベル、指摘数、修正したもの、修正しなかったものと理由）
- バージョン一覧（next-versions.sh の表）
- main に入ったときに作られるタグの一覧
- 未確認の項目のチェックリスト（Unity 6000.x / 2022.3 でのコンパイル・テスト）
- 末尾に `🤖 Generated with [Claude Code](https://claude.com/claude-code)`

worktree を片付け、PR の URL を伝えて、マージの指示を待つ。

## 6. リリース PR を作る（develop → main）

準備 PR が develop にマージされたら（ユーザーの指示でマージした場合を含む）:

1. `git fetch origin --tags --prune` し、`origin/develop` で `DRY_RUN=1 bash .github/scripts/release.sh` を実行してタグ一覧を確かめる（worktree か `git show origin/develop:...` を使い、ユーザーの作業ツリーは切り替えない）。
2. `gh pr create --base main --head develop` でリリース PR を作る。タイトルは `release: <YYYY-MM-DD>`。本文には、作られるタグの一覧、準備 PR へのリンク、マージすると release workflow がタグと Release を作ることを書く。
3. PR の URL を伝え、マージの指示を待つ。

## 7. マージ後

ユーザーの指示でリリース PR をマージしたら、`gh run list --workflow release.yml -L 1` で release workflow の実行を確認し、作られたタグと Release（`gh release list -L 10`）を報告する。失敗していたらログ（`gh run view <id> --log-failed`）を確認して原因を伝える。
