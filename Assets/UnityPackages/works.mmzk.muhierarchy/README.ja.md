[English](README.md) | 日本語

# muHierarchy

Unity の Hierarchy Window を、もう少し見やすくする Editor 拡張です。

Prefab の状態や Component のアイコン、Tag / Layer の色分けなどが Hierarchy 上に並びます。見たい情報は `Tools/muHierarchy` から表示を切り替えられます。

![Hierarchy の基本表示](Documentation~/img/component.png)

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muhierarchy
```

## Component アイコン

デフォルトの Component View では、各オブジェクトが持っている Component のアイコンが右側に並びます。Camera、Canvas、Particle など、名前を開かなくても種類が分かります。

## Prefab の未適用

Prefab インスタンスに未適用の変更があると、黄色い警告が出ます。Apply 忘れに気づきやすくなります。

![Prefab 未適用の警告](Documentation~/img/prefab-unapplied.png)

## Missing Script

Missing Script があるオブジェクトには、赤いエラーアイコンが付きます。親にも伝わるので、折りたたんだままでも探せます。

![Missing Script の表示](Documentation~/img/missing-script.png)

## Asmdef View

`Tools/muHierarchy/View Mode/Asmdef View` に切り替えると、付いているスクリプトの Assembly Definition 名が見えます。どの asmdef のコードか、Hierarchy から確認できます。

[muAsmdefgraph](../works.mmzk.muasmdefgraph/README.ja.md) と組み合わせると、asmdef 同士の参照グラフも把握しやすくなります。

![Asmdef View](Documentation~/img/asmdef-view.png)

## Reference View

`Tools/muHierarchy/View Mode/Reference View` では、オブジェクト同士の参照を線でつなぎます。親子や外部参照、Asset 参照の向きが一覧できます。
また、参照の有無がアイコン表示されます。

![Reference View](Documentation~/img/reference-view.png)

※簡易的な表示です。Inspector に見える参照が、すべて Hierarchy に乗るわけではありません。

## その他機能

### セクション見出し

`SYSTEM` / `UI` / `LEVEL` のような見出し行は、Tag の背景色でセクションとして分けられます。

### Component Name View

付いている Component の型名を右側に並べます。

### Prefab Path View

Prefab インスタンスの元アセットパスを表示します。

### Mesh / Material / Shader View

使っている Mesh、Material、Shader の名前を表示します。

どれも `Tools/muHierarchy` から切り替えられます。
