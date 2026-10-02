[English](README.md) | 日本語

# muPrimitive

開発に使う補助的なプリミティブ形状集です。GameObject にコンポーネントを追加すると形状が描画されます。範囲・向き・オブジェクト間のつながりなどのデバッグ表示や、ゲーム中の簡単な演出に使えます。

Unity 2022.3 以降。MIT License。Built-in Render Pipeline と URP で動作します。

## インストール

Unity Package Manager から Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.muprimitive
```

## 形状

どの形状も **Add Component > muPrimitive** から追加できます。Edit モードでも Play モードでも描画されます。

### PieShape

パイ型（扇形のリングに上底・下底を持たせた立体）です。攻撃範囲や地面上の視界の表示などに使えます。

| 項目 | 説明 |
| --- | --- |
| Inner Radius | 内周の半径。`0` で中心まで埋まった扇形 |
| Outer Radius | 外周の半径 |
| Angle | 開き角（度、0〜360）。ローカル +Z を中心に開く |
| Bottom / Top | 下底・上底のローカル Y 座標。同じ値なら厚みのない面になる |
| Segments | 一周あたりの分割数 |

### ConeShape

ローカル +Y 方向に伸びるコーンです。

| 項目 | 説明 |
| --- | --- |
| Radius / Height | 底面の半径と高さ |
| Apex At Origin | オフで底面が原点、オンで頂点が原点（視界やスポットライトの範囲表示向け） |
| Capped | 底面を塞ぐ |
| Segments | 一周あたりの分割数 |

### LineShape

GameObject から目標座標までを結ぶ円柱です。目標の移動に毎フレーム追従します。

| 項目 | 説明 |
| --- | --- |
| Target | 目標の Transform。未指定なら Target Position を使う |
| Target Position | 目標のワールド座標 |
| Radius | 円柱の半径（GameObject のスケールの影響を受ける） |
| Capped | 両端を塞ぐ |
| Segments | 一周あたりの分割数 |

## 見た目

すべての形状で共通の項目です。

| 項目 | 説明 |
| --- | --- |
| Color | 色。アルファが 1 未満なら半透明で描画する |
| Shading | カメラ基準の陰影の強さ（`0` で単色）。シーンにライトがなくても形が読み取れる |
| Always On Top | 他のオブジェクトに隠れず常に手前に描画する |
| Custom Material | 既定マテリアルの代わりに使うマテリアル。色は `_Color` と `_BaseColor` に渡される |

## コードからの利用

```csharp
using Mmzkworks.muPrimitive;

var pie = gameObject.AddComponent<PieShape>();
pie.InnerRadius = 0.5f;
pie.OuterRadius = 2f;
pie.Angle = 120f;
pie.Color = new Color(1f, 0.3f, 0.2f, 0.4f);

var line = gameObject.AddComponent<LineShape>();
line.Target = enemy.transform;
```

変更は `LateUpdate` で反映されます。すぐに反映したいときは `Refresh()` を呼んでください。

### 形状内のランダムな座標

`GetRandomPoint()` は形状の内部から一様分布でランダムな点をワールド座標で返します。ローカル座標が必要なら `GetRandomLocalPoint()` を使います。出現位置やパーティクルの発生位置などに使えます。

```csharp
var spawnPosition = pie.GetRandomPoint();

// System.Random を渡すと結果を再現できる（省略時は UnityEngine.Random を使う）
var random = new System.Random(seed);
var p = cone.GetRandomPoint(random);
```

同じ処理は `ShapeSampler` の静的メソッド（`SamplePie` / `SampleCone` / `SampleCylinder`）でも使えるので、コンポーネントなしでも利用できます。こちらはローカル座標で返します。

## 補足

- 各形状は同じ GameObject の `MeshFilter` と `MeshRenderer` を使います。影とプローブは無効にしています。
- 生成したメッシュはシーンに保存されず、コンポーネントの有効化時に作り直されます。
