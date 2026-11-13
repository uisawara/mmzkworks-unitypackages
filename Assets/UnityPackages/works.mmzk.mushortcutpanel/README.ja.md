[English](README.md) | 日本語

# muShortcutPanel

Project のアセットや Web ページへのショートカットとラベルを、自由なレイアウトで並べられる Editor 拡張です。

作業ごとにパネルを `<任意名>.shortcutpanel.json` として保存し、切り替えて使えます。`Tools/muShortcutPanel/ShortcutPanel` からウィンドウを開きます。

Unity 2022.3 以降。MIT License。

## インストール

Unity Package Manager から Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mushortcutpanel
```

## パネルファイル

- ツールバー左のドロップダウンに、Assets 以下にある `*.shortcutpanel.json` が一覧表示されます。選ぶと作業ファイルが切り替わります。
- ドロップダウンの `New Panel...`、または Project ウィンドウの `Create/muShortcutPanel/Shortcut Panel` で新しいパネルを作成できます。
- Project ウィンドウで `*.shortcutpanel.json` をダブルクリックすると、そのパネルを ShortcutPanel ウィンドウで直接開きます。
- 変更は自動で保存されます。

## 利用モードと編集モード

ツールバー右のトグルボタンで `Use Mode` / `Edit Mode` を切り替えます。

ツールバーの `Path` トグルで、各ショートカットの下にフルパス(アセットパス / URL / メニューパス)を表示するかを On/Off できます。

### 利用モード

- アセット / フォルダのショートカットをクリックすると、Project ウィンドウで選択されます。
- URL のショートカットをクリックすると、標準ブラウザで開きます。
- メニューコマンドのショートカットをクリックすると、そのメニューを実行します(Project / Hierarchy のコマンドはそれぞれのウィンドウの現在の選択に対して実行されます)。
- ショートカットをドラッグすると、Inspector のフィールドなどへそのままドラッグ & ドロップできます。
- 右クリックで `Copy Path` / `Copy URL` や `Open Asset` が使えます。

### 編集モード

- ショートカットやラベルをドラッグで移動、右下の角をドラッグで大きさを変更できます(8px グリッドにスナップ)。
- 何もない所からドラッグすると矩形選択で複数選択できます(`Shift` / `Ctrl`(`Cmd`)で追加選択、`Shift` / `Ctrl` クリックで個別に切り替え、`Ctrl+A` で全選択)。選択中の要素をドラッグするとまとめて移動し、`Delete`・複製・右クリックメニューも選択全体に適用されます。
- 選択中の要素は、ウィンドウ下部でテキスト・URL・タイトル・フォントサイズ・位置とサイズを編集できます。
- `+ Label` でラベルを、`+ URL` で URL ショートカットを追加します(クリップボードに URL があればそれを使います)。
- `+ Menu`(または右クリックの `Add Menu Command Here...`)で、メニューコマンドを検索して選べるピッカーが開きます。`Editor Menu` / `Project Window (Assets)` / `Hierarchy Window (GameObject)` に分かれています。
- 右クリックで複製・前面へ・背面へ・削除。`Delete` キーでも削除できます。

## ショートカットの追加

- Project ウィンドウからフォルダやファイルをドラッグ & ドロップします。
- Web ブラウザからリンク / アドレスバーの URL をドラッグ & ドロップします。`.url` / `.webloc` のインターネットショートカットファイルも受け付けます。
- URL をコピーしてパネル上で `Ctrl+V`(macOS は `Cmd+V`)でも貼り付けられます。
- 編集モードでは、`Window/General/Console`・`Assets/Create/Folder`・`GameObject/Create Empty` のようなメニューパスをコピーして `Ctrl+V` すると、メニューコマンドのショートカットとして貼り付けられます。

アセットは GUID で記録するので、移動やリネームをしても追従します。

ショートカットは高さを 56px 以上にすると、大きなアイコンのタイル表示になります。

## ファイル形式

```json
{
    "version": 1,
    "items": [
        { "id": "…", "kind": "asset", "guid": "…", "path": "Assets/Scenes", "x": 8, "y": 8, "width": 176, "height": 24, … },
        { "id": "…", "kind": "url", "url": "https://docs.unity3d.com/", "title": "Unity Docs", … },
        { "id": "…", "kind": "menu", "menu": "GameObject/Create Empty", … },
        { "id": "…", "kind": "label", "title": "Scenes", "fontSize": 14, … }
    ]
}
```
