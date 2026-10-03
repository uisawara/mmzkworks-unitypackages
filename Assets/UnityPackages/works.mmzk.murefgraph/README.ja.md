[English](README.md) | 日本語

# muRefgraph

GameObject や Prefab が持つ Component と、各 Component のフィールドが参照しているオブジェクトを、グラフで見られる Editor 拡張です。

Component を所属アセンブリ（asmdef）ごとにまとめて並べ替えることもできます。

![アセンブリ別に並べた Component と参照先](Documentation~/img/graph.png)

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.murefgraph
```

## 開き方

Hierarchy の GameObject からは、`GameObject/muRefgraph/Open Ref Graph` で開けます。

Project で Prefab を選んで `Assets/Open Ref Graph` から開けます。

`Window/muRefgraph/Ref Graph` からも開けます（選択中の GameObject が対象になります）。

## グラフ

左から、対象の GameObject、Component、フィールドが参照しているオブジェクトの順に並びます。

- 対象自身の Component だけを表示します（子の GameObject は含めません）。
- 参照は 1 段だけ辿ります。同じオブジェクトを複数のフィールドで参照している場合は 1 本の線にまとめます。
- 同じ GameObject 自身や、その別の Component を参照しているときは、オレンジの線でつなぎます。
- ノードにカーソルを合わせると、つながっている線とフィールド名が表示されます。

ノードをドラッグして並べ替え、パンとズームで全体を見渡せます。ノードをダブルクリックすると、そのオブジェクトを選択します。`Home` キーで全体を中央に戻せます。

| 操作 | 内容 |
| --- | --- |
| 左ドラッグ（ノード） | ノードの移動 |
| 左ドラッグ（背景） | 矩形選択 |
| Ctrl / Cmd + クリック | 選択の追加・解除 |
| 中ボタン / Alt + 左ドラッグ | パン |
| ホイール | ズーム |
| ダブルクリック | オブジェクトを選択して Ping |

## Group by Assembly

ツールバーの `Group by Assembly` を有効にすると、Component を型の所属アセンブリごとに枠でまとめて並べ替えます。Component の左端の色帯は、所属アセンブリごとの色です。

並べ替えた配置は、`Group by Assembly` の有効・無効それぞれで別々に保持されます。`Reset Layout` で元の配置に戻せます。

## その他機能

### Follow Selection

ツールバーの `Follow Selection` を有効にすると、Hierarchy や Project で選んだ GameObject に追従して表示を切り替えます。
