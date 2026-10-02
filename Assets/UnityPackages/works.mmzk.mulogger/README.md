English | [日本語](README.ja.md)

# muLogger

A small named logger for Unity.

- Every logger has a name, so each log line shows where it came from
- Log levels are color coded in the Editor console (plain text in player builds)
- The thread ID is shown only for logs from non-main threads
- Loggers are resolved through `LoggerLocator`, so the implementation can be swapped in one place (e.g. `NullLogger` in tests)

![Console output](Documentation~/img/logs.png)

Unity 2022.3 or later. MIT License.

## Installation

Add it from Unity Package Manager with a Git URL.

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mulogger
```

## Usage

```csharp
using System;
using Mmzkworks.muLogger;
using UnityEngine;
// UnityEngine also has an ILogger, so pick one explicitly
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

To change the logger used everywhere, replace the factory at startup (e.g. in tests).

```csharp
LoggerLocator.SetFactory(name => new NullLogger());
LoggerLocator.SetFactory(name => new UnityLogger(name) { MinimumLevel = LogLevel.Warning });
```

The factory is reset to the default (`UnityLogger`) when entering Play Mode.

| Type | Description |
| --- | --- |
| `ILogger` | Logging interface |
| `LoggerLocator` | Resolves a logger by name or type. Defaults to `UnityLogger` |
| `UnityLogger` | Writes to the Unity console. Set `MinimumLevel` to suppress lower-level logs |
| `NullLogger` | Discards all logs |
