# リリースと変更履歴

各パッケージのバージョンと `CHANGELOG.md` は、[Conventional Commits](https://www.conventionalcommits.org/ja/) の commit メッセージをもとに [release-please](https://github.com/googleapis/release-please) で管理している。

## 流れ

1. 作業ブランチから `develop` へ PR を出し、squash でマージする。
2. `develop` への push のたびに [Release workflow](../../.github/workflows/release.yml) が動き、release-please が release PR(`chore: release develop`)を作成・更新する。
   - 前回のリリース以降に、ユーザー向けの commit(`feat`・`fix`・`perf`・`revert`)があったパッケージについて、`package.json` の `version` を上げ、`CHANGELOG.md` に追記する。
   - 1.0.0 未満では、`feat` でマイナー、`fix` でパッチが上がる。
   - 対象のパッケージは、commit の scope ではなく、変更したファイルのパスで決まる。`docs`・`test`・`refactor`・`ci`・`chore` などの commit だけではリリースされない。
3. release PR を `develop` にマージすると、更新されたパッケージごとに次のものが作られる。
   - `<パッケージ名>/<version>` のタグ
   - `<パッケージ名>: <version>` という名前の GitHub Release。リリースノートの末尾には、インストール方法が追記される
   - `develop` → `main` の PR(`Promote develop to main`)
4. `develop` → `main` の PR を merge commit でマージする。

`package.json` の `version` は手で編集しない。

### ブランチの保護

- `develop`: PR 必須で、squash マージのみ。squash したときの commit メッセージは、PR タイトルだけになる(本文は空)。
- `main`: PR 必須で、merge commit のみ。`develop` → `main` を squash すると履歴が分かれてしまうため。

## PR タイトルの書き方

`develop` へは squash でマージするので、PR タイトルがそのまま commit メッセージになり、release-please はそこから CHANGELOG とバージョンを決める。

- 形は `<type>(<scope>): <説明>` か `<type>: <説明>`。破壊的変更は `<type>!:`。
- scope は、パッケージ名から `works.mmzk.` を除いたもの(例: `mudashboard`)。
- type は「UPM パッケージを取り込むユーザーに影響するか」で選ぶ。
  - `feat`・`fix`・`perf`・`revert`: 配布するパッケージの変更で、ユーザーの手元に影響するもの(機能追加、不具合修正、コンパイル警告の解消など)。CHANGELOG に載り、バージョンが上がる。
  - `docs`・`style`・`refactor`・`test`・`build`・`ci`・`chore`: CI・リポジトリの道具・開発用の Unity プロジェクト・テストなど、取り込むパッケージと関係ないもの。CHANGELOG に載らない。
- [PR title workflow](../../.github/workflows/pr-title.yml) が、タイトルの形と type、CHANGELOG に載る type の scope が既存のパッケージ名かどうかを確かめる。影響するかどうかの判断は PR の作成者が行う。
  - この検査は、ルールセットの必須チェックにはしていない。bot が作る release PR と promote PR では workflow が動かないため、必須にするとその 2 つがマージできなくなる。

### 1 PR = 1 パッケージ

PR タイトルは、その PR で変更したすべてのパッケージに適用される。そのため、1 つの PR では 1 つのパッケージだけを変更し、複数のパッケージにまたがる変更は PR を分ける。

- パッケージの `Tests` フォルダだけの変更は無視される(`release-please-config.json` の `exclude-paths`)。ただし隣にある `Tests.meta` は対象になるので、`Tests` フォルダを追加・削除する PR のタイトルは `test:` で始める。

### 1 つの PR で複数の項目を記録する

そのパッケージに複数の項目(たとえば機能追加と修正)を記録したいときは、PR 本文に空行で区切って書く。release-please は PR タイトルの代わりにこの内容を使う。

```
BEGIN_COMMIT_OVERRIDE
feat(mudashboard): ...

fix(mudashboard): ...
END_COMMIT_OVERRIDE
```

- この目印の文字列は、使うとき以外は PR 本文に書かない。説明のつもりで書いただけでも、release-please は PR タイトルの代わりに本文の続きを使ってしまう。
- マージ済みの PR の本文に書いても効く。次に release workflow が動いたときに、その PR 由来の項目が書き換わる。

## CHANGELOG の整形

`CHANGELOG.md` とリリースノートはパッケージごとに分かれているので、Release workflow が [tidy-changelog.sh](../../.github/scripts/tidy-changelog.sh) で次のように整える。

- 各行の scope 表示(muDashboard の CHANGELOG なら `**mudashboard:**`)を、そのパッケージ自身を含むときだけ消す。ほかのパッケージだけを指す scope 表示は残す。
- 空行をはさまずに並んだ 1 行だけの項目の中では、内容が同じ行を 1 行にまとめ、PR と commit へのリンクを 1 つの括弧に `,` 区切りで並べる。

判定に迷う入力は、書き換えない側に倒す。

- 複数行にわたる項目(続きの行・入れ子のリスト・コードブロック・HTML)や、本文が Markdown のブロックの記法で始まる項目を含む並びは、行が別の項目に付かないよう、生成されたまま残す(scope 表示は消す)。
- 安全に解釈できない構造(`<pre>`、HTML のコメント、開き方と閉じ方の合わないコードブロックなど)があれば、そこから後ろはすべて生成されたまま残す。
- scope 表示を消すと、本文が `#` や `>` などの Markdown の記法で始まってしまう行は、表示が変わらないよう scope 表示を残す。

整える範囲と仕組みは次のとおり。

- release PR の CHANGELOG.md は、release ブランチに整形の commit(`chore: tidy changelogs`)を積んで整える。release-please は PR 本文が変わらない限り release ブランチを書き換えないので、この commit は残る。作り直されたときは、同じ run で整え直す。
- リリースノートは、インストール方法を追記するときに整える。
- release PR の本文は、release-please が生成したままになる。

## よくある操作

### release PR の日付をリリース日に合わせる

CHANGELOG とリリースノートの日付は、release PR を最後に作り直した日になる。リリース日に合わせたいときは、release PR をマージする直前に、Actions から Release workflow を `develop` で手動実行する。

### release PR を作り直す

1. release PR を閉じる。
2. 閉じた PR から `autorelease: pending` ラベルを外す。
3. `release-please--branches--develop` ブランチを削除する。
4. Release workflow を `develop` で手動実行する。

### `develop` → `main` の PR を閉じた場合

マージせずに閉じても、リリース済みの version が `main` に入るまで、`develop` に push するたびに作り直される。

## パッケージを追加するとき

- `release-please-config.json` に、次の 2 つを付けて登録する。
  - `"initial-version": "0.1.0"`
  - `Tests` フォルダを指定した `"exclude-paths"`
- `.release-please-manifest.json` に `"0.0.0"` で登録する。
- パッケージに空の `CHANGELOG.md` と、その `.meta` を追加する。

## 設定の注意

- `release-please-config.json` の `last-release-sha` は消さない。release-please が履歴を読み始める位置を示していて、消すと muAsmdefgraph と muRefgraph で、リリース済みの commit を再び拾ってしまう。
