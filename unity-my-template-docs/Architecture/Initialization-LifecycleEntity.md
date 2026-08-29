---
title: Initialization LifecycleEntity
type: architecture
area: Foundation
module: Initialization
status: actual
source_paths:
  - Assets/Framework/Foundation/Initialization/Scripts/LifecycleEntity.cs
  - Assets/Framework/Foundation/Utilities/Scripts/EntityStatus.cs
  - Assets/Framework/Foundation/Initialization/Scripts/SceneStarter.cs
  - Assets/Framework/Foundation/Initialization/Scripts/LifecycleSceneSelector.cs
  - Assets/Framework/Foundation/Initialization/Scripts/LifecycleGate.cs
  - Assets/Framework/Foundation/Initialization/Scripts/IConditionalEntity.cs
  - Assets/Framework/Foundation/Initialization/Scripts/LifecyclePhaseTimings.cs
  - Assets/Framework/Foundation/Configs/ConfigProvider.cs
  - Assets/Framework/Foundation/Configs/ConfigReader.cs
  - Assets/Framework/Foundation/Configs/ConfigResolver.cs
  - Assets/Framework/Foundation/Configs/ConfigTypeScanner.cs
  - Assets/Framework/Foundation/Initialization/Scripts/LifecycleOrderAttribute.cs
  - Assets/Framework/Foundation/Initialization/Scripts/AutoRegistrationAttribute.cs
  - Assets/Framework/Foundation/Initialization/Scripts/AutoTypeScanner.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Extensions/VContainerBuilderExtensions.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Scopes/SceneScope.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[UI-Views]]"
  - "[[Add-UI-Window]]"
tags:
  - architecture
  - foundation
  - initialization
  - control-entity
  - vcontainer
updated: 2026-08-09
---

# Initialization LifecycleEntity

## Для агента

Используй эту статью, если нужно добавить системный компонент, понять порядок запуска сцены или разобраться с `[LifecycleOrder]`.

Текущий lifecycle:

1. `Load` — параллельно (сначала wrappers, потом bases). Здесь грузится всё, что сущности нужно снаружи: ассеты (view, canvas, audio), сейв, локализация.
2. `Init` — параллельно (сначала wrappers, потом bases).
3. `PostInit` — последовательно по порядку `LifecycleOrder`.

Отдельной фазы под конфиги нет: конфиги грузятся до всех фаз в `SceneStarter.StartAsync` (см. ниже) и доступны уже в `[Inject]`-методе сущности.

Все фазы принимают `CancellationToken` (пробрасывается из `SceneStarter.StartAsync`). В parallel-фазах wrappers выполняются до bases — это гарантирует, что `AutoViewEntity` присвоит `_view` до того, как base-сущность выполнит `Init`.

`LifecycleEntity` реализует `IDisposable`. `Dispose()` объявлен `virtual`. На scope teardown VContainer вызывает `Dispose()` базовой entity, который вызывает `Unload()` (диспозит wrapper-ы) и `Status.Dispose()`. Wrapper, которому нужна собственная очистка (например `AutoViewEntity`), реализует `IDisposableLifecycleWrapper.Dispose` явно и **обязан звать `base.Dispose()`** в конце — иначе его `EntityStatus` (R3-подписки) не диспозится. Wrapper без собственной очистки просто помечается интерфейсом — унаследованный `LifecycleEntity.Dispose()` покрывает его.

Декоратор AutoView находит сущности по интерфейсу `IAutoViewHost`, реализацию которого на компиляции генерит source generator `AutoDecorators.Generator` по полям с `[AutoWindow]` / `[AutoPopup]` (класс обязан быть `partial`). Рантайм-рефлексии в декораторе нет. Собственного gate у wrapper-а нет: загрузку view решает гейт сущности (секция «Гейт сущности»). Детали — [[UI-Views]].

Конфиги (`IConfig`) не декоратор и не фаза, а обычные зависимости контейнера: конкретный конфиг помечается `[ConfigKey("configKey")]`, регистрируется `builder.RegisterConfigs()` в `RootScope` (скан — `ConfigTypeScanner`, инстансы — `ConfigProvider`) и инжектится потребителем как `[Inject] private readonly ClickerConfig _config;`. Значение доступно уже в `[Inject]`-методе, раньше любых фаз. Код — `Assets/Framework/Foundation/Configs/`.

Источники конфига пробуются по одному — `server` → `cache` → `dummy` — и выигрывает первый, чьё значение **разобралось**, а не первый, у которого есть ключ (`ConfigResolver.Read`). Каждый провал уходит в `LogError` с ключом, именем источника и исключением. Битый `dummy` — ошибка сборки, а не рантайма: резолвер бросает, `ConfigReader` конвертирует это в `Result.HasValue = false`, `ConfigProvider.Load` — в `InvalidOperationException` с перечислением источников. Политика намеренно совпадает с [[SaveLoad]]: сбой одной единицы данных изолирован, поломка того, что лежит в сборке, падает громко.

Dummy читается не самим резолвером, а делегатом от `ConfigReader` (`ReadDummyConfig` → `IAssetProvider` → `TextAsset`). Так резолвер не зависит от Unity-типов и целиком покрывается EditMode-тестами вне редактора.

Так работает потому, что `[Inject]` синхронный, а чтение конфига — нет: `SceneStarter` первым делом ждёт `UniTask.WhenAll(_configProvider.WarmUp(ct), _clock.WarmUp(ct))` (конфиги грузятся параллельно и идемпотентно, там же синхронизируется серверное время) и только затем резолвит `IReadOnlyList<LifecycleEntity>` через `IObjectResolver`. Инжектить список entity полем нельзя — VContainer создал бы их при `Build()` scope-а, то есть до `WarmUp`.

Сущность может быть не нужна в этом запуске: `LifecycleGate` до фаз решает это по конфигу (`IConfig.IsEnabled`) и по условию самой сущности (`IConditionalEntity.ShouldRun()`), и выключенная сущность не проходит **ни одной** фазы вместе со своими обёртками — детали в секции «Гейт сущности».

Тот же генератор обрабатывает `[AutoLogger(logName, entityType = LogCategory.System, StatusLogs = false)]` **на классе**. В отличие от AutoView, рантайм-декоратора нет: генератор эмитит в partial-часть класса свойство `protected ILogChannel Logger { get; private set; }` (в sealed-классе — `private ILogChannel Logger { get; set; }`: `protected` дал бы CS0628, а `private set` у private-свойства — CS0273) и `[Inject]`-метод, который получает логгер из `ILogChannelFactory` в момент инжекта. Поэтому `[AutoLogger]` работает не только в `LifecycleEntity`, а в любом классе, который инжектит VContainer (сервисы, MonoBehaviour-view); класс обязан быть `partial`. `Logger` присвоен сразу после построения объекта контейнером — раньше любых фаз `LifecycleEntity`.

`StatusLogs = true` дополнительно вызывает `EnableStatusLogs(entityType)` в том же `[Inject]`-методе — ручной вызов в `Init` больше не нужен, а статус-логи включаются раньше фаз (ранние `SetEnabled` тоже попадают в лог). На классе, не наследующем `LifecycleEntity`, `StatusLogs = true` — compile error `ADG002`.

Ограничение `[AutoLogger]`: генератор добавляет классу свой `[Inject]`-метод, а порядок вызова нескольких `[Inject]`-методов одного типа VContainer не определяет (`TypeAnalyzer` идёт по порядку рефлексии). Поэтому классу, у которого уже есть собственный post-inject `[Inject]`-метод, атрибут не ставим — он инжектит `ILogChannelFactory` полем и берёт логгер внутри своего метода (`ConfigReader`, `AnalyticsController`, `SaveEnvelope`). Так же поступает класс, которому логгер нужен при построении внутреннего объекта (`ViewRouter` → `ViewOperationPump`). Тестовый шов для `[AutoLogger]`-класса присваивает `Logger` напрямую: сеттер приватный, но `internal`-конструктор лежит в том же типе.

`LifecycleEntity` должен быть зарегистрирован как `LifecycleEntity`, иначе `SceneStarter` его не увидит. Основной путь регистрации — `[AutoRegistration]` + `builder.RegisterAutoTypes()` в `RootScope`. Тот же атрибут работает и для обычных сервисов (не-`LifecycleEntity` регистрируются `AsSelf` + `AsImplementedInterfaces`), а все конкретные наследники `SaveBlob` регистрируются автоматически без атрибута.

`RegisterAutoTypes` сканирует не все сборки AppDomain, а только сборку с `LifecycleEntity` (`Foundation`) и те, что на неё ссылаются. Тип с `[AutoRegistration]` или наследник `LifecycleEntity`/`SaveBlob` физически не может жить в сборке без прямой ссылки на `Foundation`, поэтому фильтр ничего не теряет и пропускает Unity и сторонние плагины. Любая новая игровая сборка с такими типами обязана ссылаться на `Foundation` (она и так обязана). Скан один и кэшируется статически.

Тестовые сборки (`Foundation.Tests`/`Features.Tests`) тоже ссылаются на `Foundation` и в редакторе загружены в AppDomain, но скан их **пропускает** (по ссылке на `nunit.framework`) — иначе тестовые `SaveBlob`/`LifecycleEntity`/`[AutoRegistration]`-типы утекли бы в рантайм-контейнер и уронили бы запуск (например, тестовый `SaveBlob` без `[SaveTag]`).

Сам поиск живёт в `AutoTypeScanner.Scan(assemblies)` — чистой функции от списка сборок; `VContainerBuilderExtensions` только кэширует результат скана AppDomain и раскладывает его по регистрациям. Разделение сделано ради тестов: скан и граф регистраций покрыты `AutoTypeScannerTests`/`RegistrationGraphTests` (см. [[Testing-TDD]]), которые передают сборки явно, а не полагаются на AppDomain раннера.

## Назначение

`LifecycleEntity` — базовая единица инициализации систем проекта. Он нужен для компонентов, которым важен scene lifecycle: загрузка удалённых/локальных данных, создание runtime-объектов, подписки, регистрация UI, подготовка фич.

Обычные stateless-сервисы без фаз инициализации не должны наследоваться от `LifecycleEntity`.

## Ключевые типы

- `LifecycleEntity` — базовый класс с фазами `Load`, `Init`, `PostInit` и встроенным `EntityStatus`.
- `EntityStatus` — флаги `IsEnabled`, `IsInited`, `IsActive` с опциональным логированием смен (`IsInited` выставляет `InitPhase`, остальные — сама entity).
- `SceneStarter` — VContainer entry point, который собирает и запускает `LifecycleEntity` текущей сцены.
- `LifecycleOrderAttribute` — атрибут привязки entity к сцене и порядку.
- `AutoRegistration` — атрибут саморегистрации: entity регистрируются как `LifecycleEntity`, сервисы — `AsSelf` + `AsImplementedInterfaces`; `Lifetime.Scoped` (дефолт) означает «инстанс на сценовый scope». Сервис, который инжектят Singleton-потребители или который держит переживающий сцену кэш, обязан быть `Lifetime.Singleton` — иначе root-контейнер создаст ему отдельный инстанс (captive dependency), и потребители будут работать с двумя разными состояниями. Инвариант проверяет `RegistrationGraphTests.Graph_DoesNotCaptureScopedDependencies_InRootSingletons`.
- `VContainerBuilderExtensions.AsLifecycleEntity()` — регистрирует тип как `LifecycleEntity` и implemented interfaces.
- `SceneScope` — единый сценовый scope, добавляет `SceneStarter`; `BootstrapScope` — отдельный. Префабы scope лежат в `Initialization/Content/Scopes/` и называются `<имя константы сцены>Scope` (`BootstrapScope`, `StartScope`, `CoreScope`, `MetaScope`) — по этому правилу их и находит тест.

Сцена, на которую объявлена хотя бы одна entity, но без scope, не выполняет **ни одной** фазы: `SceneStarter` там не резолвится, ошибки и варнинга нет. Инвариант «на каждую сцену из `[LifecycleOrder]` есть scope-префаб» закрыт тестом `LifecycleSceneScopeTests.EveryLifecycleScene_HasScopePrefab` (`Assets/Framework/Features/Tests/`); соседний `EveryLifecycleScene_IsDeclaredInSceneConstants` не даёт объявить сцену литералом мимо `SceneConstants.Scenes`. Наличие **экземпляра** scope в самой сцене тест не проверяет — это остаётся ручной сверкой при заведении новой сцены.

## Как запускается сцена

`SceneStarter` параллельно прогревает конфиги и часы (`IConfigProvider.WarmUp` + `IClock.WarmUp` под одним `UniTask.WhenAll`), затем резолвит `IReadOnlyList<LifecycleEntity>` через `IObjectResolver` (именно в этот момент entity создаются и получают свои конфиги), берёт активную сцену через `SceneManager.GetActiveScene().name`, фильтрует entity по `[LifecycleOrder(sceneName, order)]`, сортирует по `InitOrder` (при равных значениях — по имени типа, чтобы порядок не зависел от порядка скана сборок), применяет `LifecycleGate`, декорирует через `LifecycleDecoratorPipeline` и запускает фазы.

Wrapper-ы, созданные декораторами, участвуют в тех же фазах, что и базовая entity. Повторный вызов `TryDecorate` для уже задекорированной entity пропускается (`Wrappers.Count > 0`) — Singleton-entity с ордерами на нескольких сценах не получает дубли wrapper-ов.

Завершение и провал старта — сигналы `SceneStarter`:

- По завершении всех фаз триггерится `SceneStartedSignal(sceneName)` — по нему скрывается loading curtain, а `GameBootstrapper` после Bootstrap-сцены входит в `StartSceneState`.
- При исключении в любой фазе вместо него уходит `SceneStartFailedSignal(sceneName, exception)`, а исключение пробрасывается дальше и логируется через `RegisterEntryPointLogging` (`VContainerBuilderExtensions`), зарегистрированный в `BootstrapScope`/`SceneScope`. По сигналу `LoadingCurtainController` снимает шторку: `SceneStartedSignal` уже не придёт, и без этого шторка висела бы поверх сцены навсегда. Полноценной UI-обработки провала (экран ошибки, ретрай) в шаблоне нет.
- Провал **самой загрузки** сцены (Addressables) фазами не покрыт: `SceneLoader` триггерит `SceneLoadFailedSignal(sceneName, exception)`, по которому `LoadingCurtainController` снимает шторку. Иначе шторка ждала бы `SceneStartedSignal`, который уже не придёт, и UI оставался бы залочен навсегда.

## Гейт сущности

`LifecycleGate.Apply` перед фазами выставляет `Status.SetEnabled` из двух источников:

1. **Конфиг** — конъюнкция `IsEnabled` всех инжектируемых в сущность конфигов (рефлексия по `[Inject]`-полям типа `IConfig`, кэш на тип).
2. **Условие** — `IConditionalEntity.ShouldRun()`, если сущность реализует этот интерфейс. Для того, что конфигом не выразить: награда за сегодня уже забрана, ивент кончился, туториал пройден.

Отказ гейта не молчаливый: `Apply` принимает `ILogChannel` (его передаёт `SceneStarter`) и на каждую **выключенную** сущность печатает одну строку с причиной —

```text
ClickerCore: disabled by ClickerConfig(IsEnabled=false)
DailyBonusCore: disabled by IConditionalEntity.ShouldRun()
```

Включённые сущности не логируются: гейт проходит каждая сущность каждой сцены, и строка на каждую была бы шумом. Разбор причины и интерполяция стоят **после** guard-а `logger.AreLogsEnabled` — иначе мусор в GC копился бы и при выключенных логах. Явного атрибута `[GatedBy]` нет намеренно: он дублировал бы то, что уже видно по `[Inject]`-полям, и его можно забыть.

`SceneStarter` не выполняет **ни одной** фазы для выключенной сущности (`LifecycleGate.IsDisabled`) — ни для базы, ни для её обёрток. То есть выключенная фича не грузит ассеты, не создаёт view и не проходит `Init`. Сущность освобождается стандартным teardown-ом scope (VContainer зовёт `Dispose()` базовой entity → `Unload()` диспозит обёртки).

Критерий — наличие источника решения (поля конфигов или `IConditionalEntity`), а не сам `IsEnabled`: `EntityStatus.IsEnabled` технически стартует с `false`, сущности без гейта функционально включены по умолчанию и обязаны явно вызвать `SetEnabled(true)` в начале своего `Init`, поэтому гейтить их по `IsEnabled` нельзя.

```csharp
public partial class DailyBonusCore : LifecycleEntity, IConditionalEntity
{
    public bool ShouldRun()
    {
        CreateModel();
        CreateAnalytics();
        // Сброс дня — в местную полночь игрока.
        _localNow = _clock.ServerLocalNow;
        return NeedToShowPopup(_localNow);
    }
}
```

Правила `ShouldRun`:

- Вызывается **один раз**, до фаз, сразу после резолва сущностей. Всё, что нужно для решения, уже готово: конфиги (`IConfigProvider.WarmUp`), серверное время (`IClock.WarmUp`), сейв (читается на Bootstrap-сцене), post-inject-зависимости вроде `AnalyticsController` и `Logger`.
- Синхронный. Условие, требующее `await`, так не выражается.
- Побочные эффекты допустимы (у `DailyBonusCore` `Evaluate` сбрасывает стрик и пишет аналитику), но должны быть безопасны при единственном вызове до фаз.
- Конфиг сильнее условия: если конфиг уже дал `false`, `ShouldRun` **не вызывается** — побочные эффекты выключенной конфигом фичи выполняться не должны.
- Если `ShouldRun` вернул `true`, все фазы идут как обычно, поэтому проверок и ранних выходов внутри `Init` не нужно.

## Порядок фаз

Фазы создаются в `SceneStarter.CreatePhases()`:

- `Load`: `runInParallel: true` — wrappers, затем bases.
- `Init`: `runInParallel: true` — wrappers, затем bases.
- `PostInit`: без `runInParallel`, значит последовательно.

Состав и порядок фаз закреплён тестом `SceneStarterTests.CreatePhases_RunsLoadInitPostInit_WithoutConfigPhase`.
Сигналов завершения фаз нет: четыре `*PhaseCompletedSignal` были удалены как механизм без подписчиков —
границы фаз наблюдаются через `SceneLoadingProgressSignal` и итоговый `SceneStartedSignal`.

В parallel-фазах wrappers всех entity выполняются параллельно, затем bases всех entity выполняются параллельно. Это **публичный контракт**, а не деталь реализации: барьер — единственное, что гарантирует `AutoViewEntity.Init` → `binding.Assign(view)` до `Init` базовой entity, то есть непустое поле view у фичи. Инвариант закреплён тестом `SceneStarterTests.ExecuteParallelPhase_StartsBases_AfterAllWrappersComplete`; рядом с барьером в `SceneStarter.ExecuteParallelPhase` стоит комментарий «почему».

Обратное неверно: закладываться на барьер как на способ упорядочить **сервисы** нельзя. Всё, от чего зависит работа wrapper-а, сервис обязан собирать в post-inject, а не в своей фазе `Init` — иначе порядок держится на везении. Так развязан `ViewRouter`: подписки на сигналы и `SetCanvasProvider` переехали из `Init` в единственный `[Inject]`-метод `InitRouter`, поэтому `Register` (его зовёт wrapper) больше не зависит от того, прошла ли фаза `Init` самого роутера.

Практическое следствие: если `Init` одной entity зависит от результата `Init` другой entity, это сейчас небезопасная зависимость. Такую зависимость нужно перенести в `PostInit`, выразить через готовый сервис/status или пересмотреть lifecycle.

`CancellationToken` пробрасывается из `SceneStarter.StartAsync` через `ExecutePhase` → `WhenAll(...).AttachExternalCancellation(ct)` → `LifecycleEntity.LoadPhase(ct)` и т.д. Наследники читают `protected CancellationToken CancellationToken { get; }` если им нужен CT.

Фаза полностью закрыта для всех сущностей до начала следующей. Практическое следствие: значение, вычисленное в `Load` любой сущности, гарантированно доступно в `Init` любой другой.

## Тайминги фаз

`SceneStarter` измеряет каждую фазу `Stopwatch`-ом и каждую сущность внутри неё
(`LifecyclePhaseTimings`). Лог завершения фазы содержит общее время и разбивку по сущностям,
отсортированную от самой медленной:

```text
Meta - Init phase completed in 412ms:
Inventory: 180ms
DailyBonusCore: 12ms
...
```

Разбивка нужна, чтобы видеть, какая сущность держит фазу, и подтверждать выигрыш от гейта замером,
а не на слово. Время сущности включает её wrapper-ы как отдельные записи.

## Как добавить LifecycleEntity

Минимальный паттерн:

```csharp
[AutoRegistration]
[LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.SomeFeature)]
public class SomeFeatureCore : LifecycleEntity, ISomeFeatureCore
{
    protected override UniTask Init()
    {
        SetEnabled(true);
        // Обязательная инициализация entity. IsInited выставит InitPhase после успешного Init.
        // Только если entity уже готова к работе и фактически активна.
        SetActive();
        return UniTask.CompletedTask;
    }
}
```

Если автоматическая регистрация не подходит, можно зарегистрировать вручную:

```csharp
builder.Register<SomeFeatureCore>(Lifetime.Scoped).AsLifecycleEntity();
```

## Статусы LifecycleEntity

У каждого `LifecycleEntity` есть `Status` типа `EntityStatus`. `IsInited` выставляет lifecycle, остальные два — сама entity.

- `IsEnabled` — сущность нужна в этом запуске. Если entity инжектит конфиг или реализует `IConditionalEntity`, значение выставляет `LifecycleGate` до фаз, поэтому повторный `SetEnabled(_config.IsEnabled)` в `Init` запрещён. Без источника решения entity функционально включена по умолчанию и обязана вызвать `SetEnabled(true)` в начале `Init`.
- `IsInited` — обязательная инициализация entity успешно завершилась. `LifecycleEntity.InitPhase` сам вызывает `Status.SetInited(true)` после того, как `Init()` завершился без исключения; ручной `SetInited()` в `Init` не нужен. Если `Init` бросил, статус остаётся `false`.
- `IsActive` — entity готова принимать работу и фактически активна. `SetActive()` вызывается в точке активации, даже если она находится позже `Init`; при деактивации вызывается `SetActive(false)`.

Сознательный ранний выход выражается явным отказом: entity, которая не довела инициализацию до конца, вызывает `SetInited(false)` до `return`. `InitPhase` запоминает любой вызов `SetInited` внутри фазы (флаг сбрасывается на входе в фазу) и не перебивает его своим `true`. Механизм покрыт тестом `LifecycleEntityTests` (`Decline`). «Попап сегодня не нужен» у `DailyBonusCore` гасится через `IConditionalEntity.ShouldRun()` до фаз; `SetInited(false)` там вызывается в `Dispose`, а не как ранний выход из `Init`.

Классы вне иерархии `LifecycleEntity`, которые держат собственный `EntityStatus` (`AudioController`, `LoadingCurtainController` — обычные `MonoBehaviour`), фаз не проходят, поэтому выставляют все свои статусы сами.

`IsInited` не включает `IsActive` автоматически: entity может успешно пройти `Init`, но стать активной позже (или не стать вовсе) — `SetActive()` всегда ручной.

Если после закрытия runtime-механика гарантированно больше не нужна в текущем scope, допустим
досрочный полный `LifecycleEntity.Dispose()`: перед ним нужно сбросить статусы. VContainer повторит
вызов при teardown scene scope — `EntityStatus` после `Dispose` переводит `Set*` и повторный
`Dispose` в no-op, поэтому статусная часть двойного Dispose безопасна; идемпотентность собственных
ресурсов (view, подписки, ассеты) остаётся обязанностью override-а. Досрочный `Dispose` освобождает
и wrapper-ы с их ассетами. `EntityStatus.Dispose()` сам по себе только освобождает подписки и не
сбрасывает последние значения флагов.

Пример без конфига:

```csharp
SetEnabled(true);
// Обязательная инициализация; успешный выход из Init сам даст IsInited.
// Entity готова к работе и активна.
SetActive();
```

Для entity с инжектируемым конфигом первая строка не нужна: `IsEnabled` уже выставлен гейтом до `Init`.

Логирование смен статусов по умолчанию выключено. Для feature-entity используй AutoLogger:

```csharp
[AutoLogger(SomeFeatureConstants.LogName, LogCategory.Feature, StatusLogs = true)]
public partial class SomeFeatureCore : LifecycleEntity
```

Для ручных сообщений об ошибках инициализации используй `Status.Logger`.

Интерфейсы фич могут наследовать `IEntityStatus` и отдавать тот же `Status` из базового `LifecycleEntity`.

`LifecycleSceneSelector.SelectForScene` — единая точка фильтрации и сортировки entity по
`[LifecycleOrder]`. Её используют и `SceneStarter`, и внешние адаптеры, которым нужен тот же
набор сущностей активной сцены.

Потребителя у селектора сейчас нет: раздел дев-оверлея, который выводил таблицу
«entity → `IsEnabled` / `IsInited` / `IsActive`», уехал вместе с платным SRDebugger
([[SRDebugger]]). Свой адаптер такую коллекцию обязан брать через `IObjectResolver` в `PostInit`:
field-injection `IReadOnlyList<LifecycleEntity>` даёт циклический resolve при построении scope.

## Инварианты

- У каждой entity, которая должна стартовать на сцене, должен быть `[LifecycleOrder]` для этой сцены.
- Entity без matching `[LifecycleOrder]` на активной сцене будет проигнорирована.
- Регистрация должна приводить тип к `LifecycleEntity`.
- Не закладывай порядок выполнения внутри параллельных фаз.
- Ошибки фаз должны падать явно: `SceneStarter` оборачивает исключение именем фазы и типом entity.
- DI в `LifecycleEntity` делается через `[Inject] private readonly` поля или VContainer post-inject метод, а не через конструктор.
- Статусы `LifecycleEntity`: `IsEnabled` — из инжектируемого конфига либо через `SetEnabled(true)` без конфига, `IsInited` — выставляет `InitPhase` после успешного `Init` (ручной вызов — только явный `SetInited(false)` на раннем выходе), `IsActive` — при фактической готовности и активности.
- Внешний debugger-адаптер, который хранит getter на `LifecycleEntity`, обязан удалить его при teardown scene scope.
- Wrapper с явной реализацией `IDisposableLifecycleWrapper.Dispose` обязан звать `base.Dispose()` в конце — `LifecycleEntity.Dispose()` виртуальный и диспозит `Status` + `Unload`. Wrapper без своей очистки не реализует `Dispose` явно — работает унаследованный.

## Частые ошибки

- Добавить `LifecycleEntity`, но забыть `[AutoRegistration]`.
- Зарегистрировать сервис (`[AutoRegistration]` без наследования `LifecycleEntity` или `RegisterScoped<T>()`), но ожидать, что он попадёт в scene lifecycle.
- Положиться на порядок `Init`, хотя `Init` сейчас параллельный.
- Использовать обычный сервис как `LifecycleEntity` только ради DI.
- Спутать VContainer `[Inject] Init()` и фазу `LifecycleEntity.Init()`.

## Когда обновлять

Обнови эту статью, если меняются:

- набор фаз или их порядок;
- параллельность/последовательность фаз;
- способ регистрации `LifecycleEntity`;
- поведение декораторов wrapper-ов;
- атрибуты `LifecycleOrderAttribute` или `AutoRegistrationAttribute`;
- scene scope lifecycle;
- статусная модель `EntityStatus` или API `SetEnabled` / `SetInited` / `SetActive`.
- способ выбора или отображения entity в runtime debugger-е.

## Last Verified

2026-08-09, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Initialization-LifecycleEntity") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Initialization-LifecycleEntity") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```