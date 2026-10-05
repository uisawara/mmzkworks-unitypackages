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

## Tag / Layer

Component View では、どの Tag・Layer かが一目で分かります(`Untagged` と `Default` はグレー。Tag Manager で名前のない Layer は `Layer 16` のように番号で出ます)。

![Tag / Layer の色帯](Documentation~/img/tag-layer-band.png)

帯をクリックするとメニューが開き、その場で Tag / Layer を変えられます。選択中のオブジェクトをクリックした場合は、選択中すべてに適用されます。Undo もできます。子を持つオブジェクトの Layer を変えるときは、Inspector と同じく子も変えるかを確認します。

[muValidation](../works.mmzk.muvalidation/README.ja.md) の SceneRules が適用されるシーンでは、ルールで禁止されている Tag / Layer はメニューでグレーアウトし、理由を併記します。対象は禁止 Tag・禁止 Layer と、Tag ごと・Component ごとの許可 Layer に反する組み合わせです。

色帯は `Tools/muHierarchy/Label Background/Enable` で切り替えられます。

### 色のカスタマイズ

`Tools/muHierarchy/Label Background/Create Color Settings` を実行すると、`Assets/Settings/muhierarchy/LabelColorSettings.asset` ができて選択されます(すでにあるときはそれを選択します)。中身は現在の色で埋まっているので、Inspector で色を変えればそのまま Hierarchy に反映されます。`Create > muHierarchy > Label Color Settings` から作ることもできます。

- **Auto colors**: 自動色の彩度(`Saturation`)・明度(`Value`)・不透明度(`Alpha`)
- **Tags / Layers**: Tag / Layer をドロップダウンで選び、背景色を指定します。`Override Text Color` をオンにすると文字色も指定できます(オフなら背景に合わせて白か黒)

リストにない Tag / Layer は自動色のままです。設定アセットが複数あるときは `Assets/Settings/muhierarchy/LabelColorSettings.asset` を、なければパス順で最初のものを使います。

## Static

Component View では、Tag / Layer の右にある `S` バッジで Static かどうかが分かります。すべての Static フラグが立っていれば青、一部だけなら薄い青、Static でなければグレーです。マウスを乗せるとフラグの内容が出ます。

バッジをクリックすると、Inspector の Static チェックボックスと同じく Static を切り替えます(全フラグをオン、すでに全フラグが立っていれば全部オフ)。選択中のオブジェクトをクリックした場合は、選択中すべてに適用されます。Undo もできます。子を持つオブジェクトでは、子も変えるかを確認します。

バッジは `Tools/muHierarchy/Show Static Icon` で表示を切り替えられます。

## Prefab の未適用

Prefab インスタンスに未適用の変更があると、黄色い警告が出ます。Apply 忘れに気づきやすくなります。

![Prefab 未適用の警告](Documentation~/img/prefab-unapplied.png)

## Missing Script

Missing Script があるオブジェクトには、赤いエラーアイコンが付きます。親にも伝わるので、折りたたんだままでも探せます。

[muValidation](../works.mmzk.muvalidation/README.ja.md) を入れている場合は、Validation 属性がエラーを報告したオブジェクトにも同じエラーアイコンが、警告だけなら(Prefab アイコンの代わりに)警告アイコンが付きます。マウスを乗せるとメッセージが出ます。muValidation の SceneRules が適用されるシーンでは、シーンのヘッダー行の右側に情報アイコンが付き、クリックするとその SceneRules アセットを選択します(複数あるときはメニューから選びます)。muValidation がなければ、表示は変わりません。

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
