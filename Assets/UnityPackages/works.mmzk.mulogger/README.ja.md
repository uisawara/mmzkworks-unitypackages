[English](README.md) | 日本語

# muLogger

Unity 用の、名前付きの小さなロガーです。

- ロガーごとに名前を付けるので、どこから出たログかが分かります
- Editor のコンソールではログレベルごとに色分けされます（プレイヤービルドではプレーンテキスト）
- メインスレッド以外から出たログにだけ、スレッド ID が付きます
- ロガーは `LoggerLocator` から取得するので、実装を1か所で差し替えられます（テストで `NullLogger` を使うなど）

![コンソール出力](Documentation~/img/logs.png)

Unity 2022.3 以降。MIT License。

## インストール

Unity の Package Manager から、Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mulogger
```

## 使い方

```csharp
using System;
using Mmzkworks.muLogger;
using UnityEngine;
// UnityEngine にも ILogger があるため、明示的に指定する
using ILogger = Mmzkworks.muLogger.ILogger;

public class Sample : MonoBehaviour
{
    private static readonly ILogger Logger = LoggerLocator.Resolve<Sample>();

    private void Start()
    {
        Logger.Log("Hello World!");
        Logger.LogWarning("Hello World!");
        Logger.LogError("Hello World!");
        Logger.LogError(new Exception("Sample exception"));
    }
}
```

全体で使うロガーを変えるときは、起動時にファクトリを差し替えます（テストなど）。

```csharp
LoggerLocator.SetFactory(name => new NullLogger());
LoggerLocator.SetFactory(name => new UnityLogger(name) { MinimumLevel = LogLevel.Warning });
```

Play Mode に入るとき、ファクトリは既定（`UnityLogger`）に戻ります。

| 型 | 内容 |
| --- | --- |
| `ILogger` | ログ出力のインターフェース |
| `LoggerLocator` | 名前または型からロガーを解決する。既定は `UnityLogger` |
| `UnityLogger` | Unity のコンソールに出力。`MinimumLevel` で指定より低いレベルを抑制 |
| `NullLogger` | 何も出力しない |
