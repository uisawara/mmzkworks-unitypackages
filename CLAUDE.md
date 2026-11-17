# mmzkworks-unitypackages

Unity の UPM パッケージを集めたモノレポ。各パッケージは `Assets/UnityPackages/<name>/` にあり、`package.json`・`Editor/` や `Runtime/`・`Tests/`・`README.md` / `README.ja.md`・`Documentation~/`・`Samples~/` で構成される。利用者は Git URL（`?path=Assets/UnityPackages/<name>`、`#<name>/<version>` でタグ指定）でインストールする。

## ブランチと PR

- `main` はリリース済みのコードだけを置く。default branch であり、タグなしでインストールした人はここの最新を受け取る。default branch は変えない（変えると ruleset の main 保護も外れる）。
- `develop` が統合ブランチ。作業ブランチは `origin/develop` から `feature/<topic>`（不具合修正は `fix/<topic>`）で切る。
- PR の base は必ず `develop`（`gh pr create --base develop`）。`main` 向けの PR は develop → main のリリース PR だけ。
- `main` / `develop` に直接 push しない（ruleset で PR 必須）。
- マージはユーザーが明示的に指示したときだけ行う。方式は merge commit（`gh pr merge <n> --merge`）。リモートのブランチは GitHub が自動削除する。
- PR の作成は `/pr`、リリースは `/release` の手順に従う。どちらもマージ前に Claude によるローカルレビューと修正を挟む。

## 作業ツリー

- このチェックアウトは Unity エディタで開かれていることが多く、テスト用アセットなど未コミットの変更が残っていることがある。ブランチの切り替えはエディタの再インポートを起こすので行わない。
- 別ブランチでの作業は、スクラッチ領域に `git worktree add -b <branch> <path> origin/develop` で作業ツリーを作って行い、終わったら `git worktree remove` する。ユーザーの作業ツリーの未コミット変更には触れない。

## コミット

- Conventional Commits 形式: `<type>(<scope>): <日本語の要約>`。scope はパッケージの短縮名（`muhierarchy` など。`works.mmzk.` を除く）。複数なら `feat(muasmdefgraph, murefgraph): …`。
- type は `feat` / `fix` / `perf` / `refactor` / `docs` / `test` / `chore` / `ci` / `style`。破壊的変更は `feat!:` のように `!` を付けるか、本文に `BREAKING CHANGE` と書く。
- type はバージョン判定に使われる（下記）ので、正しく付ける。

## バージョンとリリース

- バージョンは各パッケージの `package.json` の `version`。機能 PR では原則上げない。リリース準備のときに `bash .github/scripts/next-versions.sh` で決め、`--apply` で書き換える（`/release` が行う）。
  - 前回タグ以降にそのパッケージ配下を変更したコミットから判定する。破壊的変更 → major（0.x の間は minor）、`feat` → minor、`fix` / `perf` / `refactor` → patch、それ以外は上げない。
  - `version` が前回タグより既に進んでいれば、その値を尊重する。
- タグ `<name>/<version>` と GitHub Release は、main への push で `.github/workflows/release.yml` が自動で作る。手動でタグを打たない。`DRY_RUN=1 bash .github/scripts/release.sh` で作られる予定のタグを確認できる。

## Unity パッケージの約束

- サポートは Unity 2022.3 以降。新しい API は `#if UNITY_6000_3_OR_NEWER` などで分岐し、2022.3 でもコンパイルできるようにする。
- `Assets/` 配下に追加したファイル・フォルダは `.meta` も一緒にコミットする。`Samples~` / `Documentation~` など `~` で終わるフォルダは Unity に読み込まれないので不要。ただし `Samples~` 内でパネルなどから GUID で参照するアセットには、GUID を固定した `.meta` を置く。
- README は英語版 `README.md` と日本語版 `README.ja.md` を必ず両方更新する。
- テストは各パッケージの `Tests/`（EditMode）。エディタが開いている間は batchmode で実行できないので、`unity command test run`（Unity CLI 経由）を使うか、ユーザーに Test Runner で実行してもらう。

## スクリーンショット

- `unity command capture editor_window --window <タイトル> --output-path <path>` で撮る。撮影用のウィンドウは `unity command script eval` で開いて配置する。
- このプロジェクトは Linear カラースペースのため、撮った画像は sRGB 変換が二重にかかって白っぽくなる。`magick in.png -set colorspace sRGB -colorspace RGB -set colorspace sRGB -strip out.png` で戻してから使う。
- 置き場所は `Assets/UnityPackages/<name>/Documentation~/img/`。
