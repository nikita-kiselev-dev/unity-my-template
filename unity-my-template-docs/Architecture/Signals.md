---
title: Signals
type: architecture
area: Foundation
module: SignalBus
status: actual
source_paths:
  - Assets/Framework/Foundation/Signals/
  - Assets/Framework/Foundation/UnityLifecycle/Scripts/
  - Assets/Framework/Foundation/Scenes/Scripts/Signals/
  - Assets/Framework/Foundation/Initialization/Scripts/Signals/
related:
  - "[[UI-MVVM]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Foundation-vs-Features]]"
  - "[[Class-Interaction]]"
tags:
  - architecture
  - signalbus
  - foundation
  - r3
updated: 2026-08-09
---

# Signals

## Для агента

Открывай эту статью, когда одному месту кода нужно узнать о событии в другом, а прямой ссылки между
ними быть не должно: смена сцены, уход приложения в фон, конец показа рекламы.

Что важно решить до того, как заводить сигнал:

- **Событие или состояние?** Шина не делает replay — подписчик, пришедший позже `Trigger`, не узнает
  ничего. Для «какое сейчас значение» нужен `ReadOnlyReactiveProperty`, а не сигнал.
- **Кто-нибудь это слушает?** Сигнал без подписчиков не бесплатен: он читается как контракт, и его
  будут поддерживать. Каталог ниже помечает такие явно.
- **Хватит ли прямой зависимости?** Сигнал — способ разорвать связь между слоями, а не замена DI.
  Внутри одной фичи вызов метода честнее. Полный критерий выбора «сигнал против вызова против
  наблюдаемого свойства» — [[Class-Interaction]], там же машинное правило «сигнал заводится под
  живого подписчика».

## Назначение

`ReactiveSignalBus` — тонкая обёртка над R3: словарь `Type → Subject<T>`, ничего больше
(`ReactiveSignalBus.cs:20-68`). Он существует ради двух вещей: единого правила именования и чистки
подписок, и ради того, чтобы `Foundation`-инфраструктура (шторка, сохранение, часы) реагировала на
события, не зная своих источников.

## Модель: payload-in-signal

Стрим идентифицируется **только типом сигнала**. Отдельного generic-параметра под данные нет —
данные лежат в полях самого сигнала. Любой подписчик на `T` получает каждый `T`.

**Маркер-сигнал** (данных нет) — пустой класс:

```csharp
_signalBus.Trigger<ApplicationQuittingSignal>();
_signalBus.Subscribe<ApplicationQuittingSignal>(OnApplicationQuit).AddTo(ref _subscriptions);
```

`Trigger<T>()` требует `new()` и создаёт инстанс сам.

**Payload-сигнал** — данные в полях:

```csharp
_signalBus.Trigger(new SceneStartedSignal(sceneName));
_signalBus.Subscribe<SceneStartedSignal>(signal => Handle(signal.SceneName)).AddTo(ref _subscriptions);
```

`Subscribe<T>(Action)` — перегрузка для случая, когда payload есть, но подписчику он не нужен.

## Каталог сигналов шаблона

Таблица — источник истины по тому, кто что публикует. Пустая колонка «Слушают» означает, что сигнал
сейчас никем не потребляется, и это машинная находка `interaction-check`:
каждый такой сигнал либо удаляется, либо получает строку в `Tools/interaction-check.exceptions.txt`
с объяснением, какая точка расширения его ждёт.

| Сигнал | Payload | Триггерит | Слушают |
| --- | --- | --- | --- |
| `ApplicationPauseChangedSignal` | `bool` | `UnityLifecycleRelay` | `ProgressSaver`, `Clock` |
| `ApplicationQuittingSignal` | — | `UnityLifecycleRelay` | `ProgressSaver` |
| `SceneChangeRequestedSignal` | — | `SceneLoader` | `LoadingCurtainController` |
| `LoadingCurtainShownSignal` | — | `LoadingCurtainController` | `SceneService`, `AddressableAssetProvider`, `IconProvider` |
| `LoadingCurtainHiddenSignal` | — | `LoadingCurtainController` | `ViewRouter` |
| `SceneLoadingProgressSignal` | `float` | `SceneLoadingProgressReporter` | `LoadingCurtainController` |
| `SceneLoadFailedSignal` | имя сцены, `Exception` | `SceneLoader` | `LoadingCurtainController` |
| `SceneChangedSignal` | — | `SceneService` | `ProgressSaver` |
| `SceneStartedSignal` | имя сцены | `SceneStarter` | `GameBootstrapper`, `LoadingCurtainController` |
| `SceneStartFailedSignal` | имя сцены, `Exception` | `SceneStarter` | `LoadingCurtainController` |
| `PopupBackgroundClickedSignal` | — | `CanvasProvider` | `ViewRouter` |
| `AdStartedSignal` | формат | `AdsController` | — |
| `AdFinishedSignal` | формат, `AdResult` | `AdsController` | — |
| `ServerLoginCompletedSignal` | — | — | `ConfigReader` |

Асимметричный случай один: `ServerLoginCompletedSignal` — единственный сигнал **с подписчиком, но
без источника**. Его триггерит интеграция LiveOps, которой в шаблоне сейчас нет; `ConfigReader` по
нему перечитывает серверные конфиги (`ConfigReader.cs:105`), см. [[LiveOps]] и [[Configs]].

Пустая колонка «Слушают» осталась только у `AdStartedSignal` / `AdFinishedSignal` — это объявленная
граница расширения, `Foundation` не вправе знать игровой код, который ставит геймплей на паузу
([[Ads]]). Обе строки лежат в постоянной части `Tools/interaction-check.exceptions.txt`.

`UnityAwakeSignal`, `UnityStartSignal` и `ApplicationFocusChangedSignal` удалены:
подписчиков не было, а у первых двух не могло и появиться — шина без replay, и
Awake/Start релея происходят раньше, чем кто-либо успевает подписаться. Реальные сценарии
(сейв, ресинк часов) закрывает `ApplicationPauseChangedSignal`.

## Отсутствие replay

`Trigger` без активных подписчиков **молча теряется**: если стрима для типа ещё нет,
`Trigger` вообще ничего не делает (`ReactiveSignalBus.cs:32-46`). Практические следствия:

- Сигнал, который триггерится из Unity-коллбека самого раннего объекта сцены, получить некому:
  подписчиков к этому моменту ещё нет. Именно поэтому `UnityAwakeSignal` / `UnityStartSignal`
  удалены, а не помечены исключением.
- Сигнал не годится как «флаг готовности». Готовность выражается свойством у владельца
  (`IClock.Trust`, `EntityStatus.IsInited`), а не пойманным когда-то событием.

## Чистка подписок

`Subscribe` возвращает `IDisposable`. Правило общее для сигналов и любых R3-стримов:
**любой `Subscribe()` немедленно заканчивается `.AddTo(...)`**.

| Где | Как |
| --- | --- |
| `MonoBehaviour` | `.AddTo(this)` — Unity чистит при уничтожении объекта |
| Обычный класс | поле `private DisposableBag _subscriptions` + `.AddTo(ref _subscriptions)`, в `Dispose` — `_subscriptions.Dispose()` |
| `ViewModel` | `.AddTo(ref Subscriptions)` — bag уже есть в базовом классе, см. [[UI-MVVM]] |

`DisposableBag` — struct, поэтому только через `ref`: передача по значению создаст копию, и реальные
подписки не почистятся.

Сама шина `IDisposable`: `Dispose` завершает и диспозит все `Subject` (`ReactiveSignalBus.cs:48-56`).
Она Singleton, так что на практике это происходит на teardown root-контейнера.

## Инварианты

- Имя сигнала — **прошедшее время**, суффикс `Signal` обязателен, префикс `On` запрещён:
  `AdStartedSignal`, `SceneChangeRequestedSignal`. Проверяется правилами `signal-suffix` и
  `signal-on-prefix` в `Tools/naming-check.ps1`.
- Каждый сигнал реализует `ISignal` — без этого он не компилируется в `Trigger`/`Subscribe`.
- За каждым `Subscribe(` в той же строке или на следующей стоит `.AddTo(`. Проверка:
  `grep -rn "Subscribe<" Assets/Framework --include=*.cs | grep -v "AddTo" | grep -v "/Tests/"` —
  попадания только в `ISignalBus.cs` и `ReactiveSignalBus.cs` (объявления перегрузок). В тестах
  подписка живёт до конца теста, `AddTo` там не требуется.
- Шина не потокобезопасна: `Trigger` и `Subscribe` вызываются только из главного потока Unity
  (`ReactiveSignalBus.cs:9-16`).
- Сигнал не используется как источник текущего состояния — только как уведомление о переходе.
- Payload сигнала immutable: поля только для чтения, заполняются в конструкторе.

## Как расширять

**Новый сигнал.** Класс с суффиксом `Signal`, реализует `ISignal`, лежит в `Signals/` рядом с
источником — в `Foundation/<Подсистема>/Signals/` или рядом с feature-кодом в `Features/`. Данных
нет — пустой класс и `Trigger<T>()`; данные есть — readonly-поля и `Trigger(new T(...))`.

**Исключение в обработчике** не валит триггер: R3 изолирует его и отдаёт в
unhandled-exception handler `ObservableSystem` (в Unity — `Debug.LogException`). Это значит, что
подписчик, который упал, **не** отменяет доставку остальным, но и не сообщает об этом источнику.
Если результат обработки важен источнику — это не сигнал, а вызов метода через интерфейс.

**Фильтрация и композиция.** Шина отдаёт `IDisposable`, а не `Observable`, поэтому операторы R3
(`Where`, `Throttle`, `CombineLatest`) к сигналу напрямую не применяются. Понадобится — контракт
расширяется методом `Observable<T> AsObservable<T>()`, а не обходом шины через свой `Subject`.

## Тесты

`Assets/Framework/Foundation/Tests/ReactiveSignalBusTests.cs` — пять инвариантов шины: доставка
маркера и payload-а, изоляция стримов по типу, остановка доставки после `Dispose` подписки и
изоляция исключения обработчика (упавший подписчик не мешает следующему, исключение уходит в
`ObservableSystem`-хендлер). Сигналы для тестов объявлены прямо в файле — реальные не нужны.

В тестах чужих подсистем шина используется как есть, без фейка: она не требует Unity и создаётся
обычным `new ReactiveSignalBus()` (`AdsControllerTests`, `SceneLoaderTests`).

## Когда обновлять

- Добавлен или удалён сигнал — таблица «Каталог сигналов шаблона» обязана оставаться полной.
- У сигнала появился первый подписчик или исчез последний (колонка «Слушают»).
- Изменился контракт `ISignalBus` (новая перегрузка, отдача `Observable`).
- Появилась реализация LiveOps, которая начала триггерить `ServerLoginCompletedSignal`.
- Изменилось правило чистки подписок в [[UI-MVVM]] — здесь оно продублировано.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Signals") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Signals") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
