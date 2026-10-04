[English](README.md) | 日本語

# muEventHub

Actor 同士の接触などを「起きた事象（Event）」として EventHub に送り、「その結果起こること（Rule）」を別に書くためのイベントハブです。

- **Event**: 「A が B に触れた」のような、起きた事実だけを表す `readonly struct` です。名前は過去形にします。
- **Rule**: Event を受け取り、取得・ダメージ・破棄・別の Event の発行などを決めます。

Actor 側は事実を送るだけなので、プレハブにゲームルールを持たせずに済みます。ルールは差し替えやテストがしやすくなり、因果関係をログで追えます。

イベントの発行と処理では GC 割り当てが発生しません（[ゼロアロケーション](#ゼロアロケーション)を参照）。

Unity 2022.3 以降。MIT License。

## インストール

[muLogger](../works.mmzk.mulogger/README.ja.md) と [muValidation](../works.mmzk.muvalidation/README.ja.md) に依存しています。先にこれらを入れてください。

そのうえで、Unity の Package Manager から Git URL で追加します。

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mueventhub
```

## 処理の流れ

```
ContactSensor ──Publish(ContactEvent)──▶ EventHub（キュー）
                                          │ LateUpdate で Dispatch()
                                          ▼
                              IEventRule<ContactEvent> 群
                                 └─ context.Publish(GemCollectedEvent) ──▶ 同じ Dispatch の中で続けて処理
```

- `Publish` はキューに積むだけです。ルールは `Dispatch()` のときに実行されます。物理コールバックの中で Destroy などを直接行わずに済みます。
- イベントは FIFO で、ルールは登録順に処理します。ルールが発行した後続のイベントは、同じ `Dispatch` の中で処理します。
- 型は厳密に一致したものだけに配信します。基底型やインターフェイスで購読しても届きません。
- ルールの中で `Subscribe` や `Dispose` をしても、反映は次のイベントからです。
- 1 つのルールが例外を投げても、ほかのルールとイベントの処理は続きます。
- 無限連鎖を防ぐための上限があります。
  - `MaxChainDepth`（既定 16）: これより深い後続イベントは破棄します。
  - `MaxEventsPerDispatch`（既定 1024）: これを超えた分は次の `Dispatch` に回します。

## 使い方

### 1. シーンの準備

1. 常駐シーンに `EventHubRunner` を置きます。ほかのスクリプトより後の `LateUpdate` で `Dispatch` します。`EventHubRunner.Current` とその `Hub` は Runner の `Awake` より前から使えるので、同じシーンのコンポーネントが自分の `Awake` や `OnEnable` で購読できます。`Log Events` をオンにすると、イベントの因果をコンソールに出力します。
2. Actor に `EventActor` を付けて、`Kind`（例: `Player`、`Gem`）を設定します。
3. 接触を検知したい Actor に `ContactSensor` を付けます。触れた相手が `EventActor` を持っていれば `ContactEvent(Self, Other, Phase, IsTrigger, Point)` を発行します。

### 2. Event を定義する

```csharp
using Mmzkworks.muEventHub;

public readonly struct GemCollectedEvent : IEvent
{
    public EventActor Collector { get; }
    public EventActor Gem { get; }

    public GemCollectedEvent(EventActor collector, EventActor gem)
    {
        Collector = collector;
        Gem = gem;
    }
}
```

### 3. Rule を書いて登録する

```csharp
public sealed class GemPickupRule : IEventRule<ContactEvent>
{
    public void Handle(in ContactEvent e, IEventContext context)
    {
        if (e.Self == null || e.Other == null) return;
        if (e.Self.Is("Gem") && e.Other.Is("Player"))
        {
            context.Publish(new GemCollectedEvent(e.Other, e.Self));
        }
    }
}

var hub = EventHubRunner.Resolve();
IDisposable subscription = hub.Subscribe(new GemPickupRule());
hub.Subscribe<GemCollectedEvent>(e => Object.Destroy(e.Gem.gameObject));

// 解除
subscription.Dispose();
```

`OnEnable` で登録して `OnDisable` で `Dispose` するインストーラーをレベルのシーンに置くと、レベルごとにルールを差し替えられます。

### メソッドチェインで Rule を書く

イベントを絞り込んで変換するルールは、`hub.On<TEvent>()` から始まるメソッドチェインでも書けます。

```csharp
IDisposable subscription = hub.On<ContactEvent>()
    .Where(e => e.Phase == ContactPhase.Enter)
    .Where(e => e.Self != null && e.Other != null)
    .Where(e => e.Self.Is("Gem") && e.Other.Is("Player"))
    .DistinctBy(e => e.Self.ActorId)
    .Publish(e => new GemCollectedEvent(e.Other, e.Self));

// 下の ContactEvent 用フィルタで書いた同じルール
hub.On<ContactEvent>()
    .WhereContactPhaseIsEnter()
    .WhereContactSelfAndOtherKindsAre("Gem", "Player")
    .DistinctByContactSelfActor()
    .Publish(e => new GemCollectedEvent(e.Other, e.Self));
```

| メソッド | 説明 |
| --- | --- |
| `Where(predicate)` | 条件が true のイベントだけを通す |
| `Select(selector)` | イベントを変換する(型は自由) |
| `DistinctBy(keySelector)` | キーごとに最初のイベントだけを通す。キーは購読中ずっと保持する |
| `Publish(selector)` | 購読する。変換したイベントを後続イベントとして発行する(原因と深さを引き継ぐ) |
| `Subscribe(action)` | 購読する。`action(e)` か `action(e, context)` を呼ぶ |

`Publish` か `Subscribe` を呼ぶまでは購読しません。どちらも `IDisposable` を返します。チェインはこのとき 1 回だけ組み立てるので、ラムダからフィールドやローカル変数を参照してもかまいません。イベントの処理では組み立て済みの処理を順に実行するだけで、割り当ては発生しません。

#### ContactEvent 用のフィルタ

`ContactEvent` でよく使う絞り込みを用意しています。どれも `WhereContact` / `DistinctByContact` で始まるので、コード補完でまとめて探せます。

| メソッド | 通す条件 |
| --- | --- |
| `WhereContactPhaseIsEnter()` / `WhereContactPhaseIsStay()` / `WhereContactPhaseIsExit()` | `Phase` が Enter / Stay / Exit |
| `WhereContactPhaseIs(phase)` | `Phase` が `phase` |
| `WhereContactIsTrigger()` / `WhereContactIsCollision()` | トリガーの接触 / 衝突 |
| `WhereContactActorsAreAlive()` | `Self` も `Other` も破棄されていない |
| `WhereContactSelfKindIs(kind)` / `WhereContactOtherKindIs(kind)` | `Self` / `Other` の `Kind` が一致する |
| `WhereContactSelfAndOtherKindsAre(selfKind, otherKind)` | 上の 2 つを両方満たす |
| `DistinctByContactSelfActor()` | その `Self` の最初の接触 |
| `DistinctByContactSelfAndOtherActors()` | その (`Self`, `Other`) の組の最初の接触 |

- Kind と `DistinctByContact` のフィルタは、Actor が破棄されていれば通しません。そのため前に `WhereContactActorsAreAlive()` を書く必要はありません。
- `DistinctByContact` のフィルタは、購読中ずっとキーを保持します。
- Kind が空のときは、チェインを組み立てる時点で `ArgumentException` になります。

### 監視

`EventHub.Observer` に `IEventHubObserver` を設定すると、発行、処理完了、エラーを受け取れます。どの呼び出しにも `EventInfo`（`Id`、`EventType`、`CauseId`、`CauseType`、`Depth`）が渡されるので、因果関係をたどれます。ルールの中では同じ情報を `context.Info` で参照できます。`LoggingEventHubObserver` は因果関係を、muLogger の `ILogger` にインデント付きで出力します（割り当てが発生するので、デバッグ専用です）。

### ログ

ログは muLogger の `ILogger` に出力します。既定では `LoggerLocator` から取得します。

- `Observer` を設定していないとき、`EventHub` はルールの例外を `LogError` で、上限に達したことを `LogWarning` で出力します。
- `EventHubRunner`、`ContactSensor`、`LoggingEventHubObserver` も、設定の不備やイベントの流れを同じ方法で出力します。

コンストラクタで独自のロガーを渡すか、`LoggerLocator.SetFactory` で全体を差し替えられます。

```csharp
var hub = new EventHub(initialCapacity: 128, logger: new NullLogger());
hub.Observer = new LoggingEventHubObserver(new UnityLogger("Events"));
```

### Validation

各コンポーネントには [muValidation](../works.mmzk.muvalidation/README.ja.md) の属性を付けています。設定の不備は Project ウィンドウ、Hierarchy、Validation ウィンドウに表示され、ビルドを止めることもできます。

| コンポーネント | チェック内容 |
| --- | --- |
| `EventActor` | `Kind` が空でない |
| `ContactSensor` | 同じ GameObject か親に `EventActor` がある |
| `ContactSensor` | 同じ GameObject に Collider か Rigidbody がある(ないと Unity が接触を通知しない) |
| `EventHubRunner` | シーンに 1 つだけ |

## ゼロアロケーション

ウォームアップが済んだあとは、`Publish` と `Dispatch` で割り当ては発生しません。

- イベントは struct で、イベントの型ごとのキューに入れるので、ボクシングされません。
- ルールは `in` でイベントを受け取り、Observer のメソッドはジェネリックです。
- ルールの一覧をコピーするのは、購読するときだけです。

割り当てが発生するのは次の場合です。

- `Subscribe`（購読オブジェクトと、ルール一覧のコピー）。購読は毎フレームではなく、シーンやレベルの開始時に行ってください。
- 型ごとの最初のイベントと、キューが今の容量を超えて伸びるとき。`new EventHub(initialCapacity)`（または `EventHubRunner` の `Initial Capacity`）で、あらかじめ容量を確保できます。
- エラーや上限の警告（例外とメッセージ）。
- `Subscribe(Action<TEvent>)` とメソッドチェインは問題ありません。ラムダは購読時に 1 回だけ作られます。`Handle` などイベントごとに通るコードの中で、変数をキャプチャするラムダを作るのは避けてください。
- `ContactSensor` は Unity の物理コールバックを使います。`Physics.reuseCollisionCallbacks` を有効のまま（既定）にしておけば、`OnCollision*` のたびに `Collision` が割り当てられることはありません。

## 注意

- メインスレッドからだけ使ってください。
- Event は Actor を参照しますが、`Dispatch` までに Actor が破棄されている場合があります。ルールでは `== null` で確認してください。
- 両方の Actor に `ContactSensor` があると、1 回の接触で 2 つのイベント（A→B と B→A）が発行されます。センサーはどちらか片方にだけ付けるか、ルールで向きを判定してください。
- 同じフレームで複数の接触が起きることがあります。1 回だけ処理したい場合は、ルールの側で重複を除いてください。
- `IEventContext` は `Handle` の中でだけ有効です。保持して後から `Publish` すると例外になります。
