English | [日本語](README.ja.md)

# muEventHub

An event hub that separates **what happened** from **what it causes**. Actors publish facts (such as "A touched B") as events, and rules registered on the hub decide the consequences.

- **Event**: a `readonly struct` describing something that has already happened. Name it in the past tense.
- **Rule**: receives an event and decides what follows — pick-up, damage, despawn, or publishing further events.

Actors only report facts, so prefabs stay free of game rules. Rules are easy to swap and test, and the cause-and-effect chain can be logged.

Publishing and dispatching do not allocate GC memory (see [Zero allocation](#zero-allocation)).

Unity 2022.3 or later. MIT License.

## Installation

This package depends on [muLogger](../works.mmzk.mulogger/README.md) and [muValidation](../works.mmzk.muvalidation/README.md). Install them first.

Then add it from the Unity Package Manager using a Git URL:

```
https://github.com/uisawara/mmzkworks-unitypackages.git?path=Assets/UnityPackages/works.mmzk.mueventhub
```

## How it works

```
ContactSensor ──Publish(ContactEvent)──▶ EventHub (queue)
                                          │ Dispatch() in LateUpdate
                                          ▼
                              IEventRule<ContactEvent> rules
                                 └─ context.Publish(GemCollectedEvent) ──▶ handled later in the same Dispatch
```

- `Publish` only queues. Rules run in `Dispatch()`, so you never destroy objects from inside physics callbacks.
- Events are processed FIFO, rules in subscription order. Follow-up events published by rules are processed in the same `Dispatch`.
- Delivery is by exact event type; subscribing to a base type or interface does not receive derived events.
- `Subscribe` / `Dispose` inside a rule takes effect from the next event.
- A rule that throws does not stop other rules or events.
- Limits prevent runaway chains:
  - `MaxChainDepth` (default 16): deeper follow-up events are dropped.
  - `MaxEventsPerDispatch` (default 1024): the rest is deferred to the next `Dispatch`.

## Usage

### 1. Set up the scene

1. Put an `EventHubRunner` in a resident scene. It dispatches in `LateUpdate`, after other scripts. `EventHubRunner.Current` and its `Hub` are available even before its `Awake`, so components in the same scene can subscribe in their own `Awake` / `OnEnable`. Enable `Log Events` to print the event chain to the console.
2. Add an `EventActor` to each actor and set its `Kind` (e.g. `Player`, `Gem`).
3. Add a `ContactSensor` to actors whose contacts matter. When it touches another `EventActor`, it publishes `ContactEvent(Self, Other, Phase, IsTrigger, Point)`.

### 2. Define an event

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

### 3. Write and register rules

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

// Unsubscribe
subscription.Dispose();
```

An installer that subscribes in `OnEnable` and disposes in `OnDisable`, placed in each level scene, lets each level have its own rules.

### Rules as a method chain

A rule that filters and converts events can be written as a chain starting from `hub.On<TEvent>()`:

```csharp
IDisposable subscription = hub.On<ContactEvent>()
    .Where(e => e.Phase == ContactPhase.Enter)
    .Where(e => e.Self != null && e.Other != null)
    .Where(e => e.Self.Is("Gem") && e.Other.Is("Player"))
    .DistinctBy(e => e.Self.ActorId)
    .Publish(e => new GemCollectedEvent(e.Other, e.Self));

// Same rule with the ContactEvent filters below
hub.On<ContactEvent>()
    .WhereContactPhaseIsEnter()
    .WhereContactSelfAndOtherKindsAre("Gem", "Player")
    .DistinctByContactSelfActor()
    .Publish(e => new GemCollectedEvent(e.Other, e.Self));
```

| Method | Description |
| --- | --- |
| `Where(predicate)` | Passes only events for which the predicate returns true |
| `Select(selector)` | Transforms each event (into any type) |
| `DistinctBy(keySelector)` | Passes only the first event for each key. Keys are kept while subscribed |
| `Publish(selector)` | Subscribes; publishes the selected event as a follow-up (cause and depth carry over) |
| `Subscribe(action)` | Subscribes; calls `action(e)` or `action(e, context)` |

Nothing is subscribed until `Publish` or `Subscribe` is called; both return an `IDisposable`. The chain is built once at that point, so the lambdas may capture fields and locals: handling an event only runs the built stages and does not allocate.

#### ContactEvent filters

Common filters for `ContactEvent` flows. They all start with `WhereContact` / `DistinctByContact`, so code completion lists them together.

| Method | Passes when |
| --- | --- |
| `WhereContactPhaseIsEnter()` / `WhereContactPhaseIsStay()` / `WhereContactPhaseIsExit()` | `Phase` is Enter / Stay / Exit |
| `WhereContactPhaseIs(phase)` | `Phase` is `phase` |
| `WhereContactIsTrigger()` / `WhereContactIsCollision()` | The contact is a trigger / a collision |
| `WhereContactActorsAreAlive()` | Neither `Self` nor `Other` has been destroyed |
| `WhereContactSelfKindIs(kind)` / `WhereContactOtherKindIs(kind)` | `Self` / `Other` has the `Kind` |
| `WhereContactSelfAndOtherKindsAre(selfKind, otherKind)` | Both of the above |
| `DistinctByContactSelfActor()` | It is the first contact of this `Self` |
| `DistinctByContactSelfAndOtherActors()` | It is the first contact of this (`Self`, `Other`) pair |

- Kind and `DistinctByContact` filters reject contacts whose actor has been destroyed, so `WhereContactActorsAreAlive()` is not needed before them.
- `DistinctByContact` filters keep their keys while subscribed.
- An empty Kind throws `ArgumentException` when the chain is built.

### Monitoring

Set `EventHub.Observer` to an `IEventHubObserver` to receive publishes, handled events and errors. Each call gets an `EventInfo` (`Id`, `EventType`, `CauseId`, `CauseType`, `Depth`) so you can follow the cause-and-effect chain. Inside a rule, the same information is available as `context.Info`. `LoggingEventHubObserver` writes the chain to a muLogger `ILogger`, indented by depth (it allocates; use it for debugging only).

### Logging

Logs go through muLogger's `ILogger`, resolved from `LoggerLocator` by default:

- When no `Observer` is set, `EventHub` reports rule exceptions with `LogError` and limit hits with `LogWarning`.
- `EventHubRunner`, `ContactSensor` and `LoggingEventHubObserver` log setup problems and event flow the same way.

Pass your own logger to the constructor, or replace it everywhere with `LoggerLocator.SetFactory`:

```csharp
var hub = new EventHub(initialCapacity: 128, logger: new NullLogger());
hub.Observer = new LoggingEventHubObserver(new UnityLogger("Events"));
```

### Validation

The components carry [muValidation](../works.mmzk.muvalidation/README.md) attributes, so setup mistakes show up in the Project window, the Hierarchy and the Validation window, and can stop a build.

| Component | Check |
| --- | --- |
| `EventActor` | `Kind` is not empty |
| `ContactSensor` | An `EventActor` is on the same GameObject or a parent |
| `ContactSensor` | A Collider or Rigidbody is on the same GameObject (otherwise Unity never sends contacts) |
| `EventHubRunner` | Only one per scene |

## Zero allocation

Once warmed up, `Publish` and `Dispatch` allocate nothing:

- Events are structs, kept in a queue per event type, so they are never boxed.
- Rules take events by `in`, and observer methods are generic.
- Rule lists are copied only when subscribing.

What still allocates:

- `Subscribe` (the subscription and the copied rule list). Subscribe when a scene or level starts, not every frame.
- The first event of each type, and queues growing beyond their size. Pass `new EventHub(initialCapacity)` (or set `Initial Capacity` on `EventHubRunner`) to size them up front.
- Errors and limit warnings (exceptions and messages).
- `Subscribe(Action<TEvent>)` and method chains are fine: their lambdas are created once when subscribing. Avoid creating capturing lambdas inside `Handle` or other per-event code.
- `ContactSensor` relies on Unity physics callbacks. Keep `Physics.reuseCollisionCallbacks` enabled (the default) so `OnCollision*` does not allocate a `Collision` per call.

## Notes

- Use from the main thread only.
- Events reference actors that may be destroyed before `Dispatch`; check them with `== null` in rules.
- If both actors have a `ContactSensor`, one contact produces two events (A→B and B→A). Put the sensor on one side only, or check the direction in the rule.
- Several contacts can happen in one frame. If something should happen only once, deduplicate in the rule.
- `IEventContext` is only valid inside `Handle`; publishing through a stored context throws.
