---
title: UI ViewRouter
type: architecture
area: Foundation
module: UI
status: actual
source_paths:
  - Assets/Framework/Foundation/UI/Views/ViewRouter.cs
  - Assets/Framework/Foundation/UI/Views/IViewRouter.cs
  - Assets/Framework/Foundation/UI/Views/MonoView.cs
  - Assets/Framework/Foundation/UI/Views/ViewKind.cs
  - Assets/Framework/Foundation/UI/Views/ViewState.cs
  - Assets/Framework/Foundation/UI/Views/ViewWrapper.cs
  - Assets/Framework/Foundation/UI/Views/WindowQueue.cs
  - Assets/Framework/Foundation/UI/Views/PopupStack.cs
  - Assets/Framework/Foundation/UI/Views/ViewOperation.cs
  - Assets/Framework/Foundation/UI/Views/ViewOperationPump.cs
  - Assets/Framework/Foundation/UI/Views/IViewOperationExecutor.cs
  - Assets/Framework/Foundation/UI/Views/ViewRegistration.cs
  - Assets/Framework/Foundation/UI/Views/ViewAnimation/IViewAnimator.cs
  - Assets/Framework/Foundation/UI/Views/ViewFactory.cs
  - Assets/Framework/Foundation/UI/Views/IViewFactory.cs
  - Assets/Framework/Foundation/UI/Canvas/Scripts/CanvasProvider.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/AutoWindowAttribute.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/AutoPopupAttribute.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/AutoViewEntity.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/AutoViewBinding.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/IAutoViewHost.cs
  - Assets/Framework/Foundation/Initialization/Scripts/IConditionalEntity.cs
  - Tools/AutoDecorators.Generator/AutoDecoratorsGenerator.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[UI-MVVM]]"
  - "[[Add-UI-Window]]"
tags:
  - architecture
  - foundation
  - ui
  - view-manager
  - auto-view
updated: 2026-08-26
---

# UI ViewRouter

## Для агента

Используй эту статью перед работой с окнами, popup-ами, `MonoView`, `ViewRouter`, `AutoWindow` / `AutoPopup` или canvas-ами.

В большинстве игровых фич view создаётся через `[AutoWindow]` / `[AutoPopup]`: prefab загружается, создаётся через `ViewFactory`, регистрируется в `ViewRouter`, а поле в `LifecycleEntity` заполняется автоматически.

UI-логика фич пишется по MVVM: view наследуется от `MonoView<TViewModel>` и биндится к ViewModel — см. [[UI-MVVM]]. Эта статья описывает инфраструктуру показа (каналы, очередь, фабрику, AutoWindow/AutoPopup).

Для добавления нового окна сначала открой [[Add-UI-Window]].

## Назначение

UI-подсистема разделяет:

- создание view из prefab-ов;
- выбор canvas-а по `ViewKind`;
- регистрацию view по ключу;
- открытие и закрытие окон/popup-ов;
- popup stack и затемнение фона;
- lifecycle view внутри scene initialization.

## Ключевые типы

- `IViewRouter` — публичный контракт открытия, закрытия и регистрации view.
- `ViewRouter` — registry view + `ViewOperationPump` (coalesce + сериализация операций); делегирует показ/скрытие каналам.
- `ViewOperationPump` — очередь операций: после idle ждёт 1 кадр, затем дренит FIFO; подряд идущие `Open` popup — batch.
- `IViewOperationExecutor` — что делать с операцией, когда до неё дошла очередь (`internal`, реализует `ViewRouter` явно). Pump знает порядок и коалесинг, но не знает про `WindowQueue` и `PopupStack`.
- `WindowQueue` — канал окон: одно текущее окно + FIFO-очередь ожидающих.
- `PopupStack` — канал попапов: swap-стек (виден только верхний) + `OpenBatch` + управление фоном.
- `ViewState` — состояние view: `Closed`, `Open`, `Suspended` (public, виден фичам через `MonoView.State`).
- `MonoView` — базовый класс view-компонента, даёт `Open()` / `Close()`, lifecycle-стримы `State` / `OnOpen` / `OnOpened` / `OnClose` / `OnClosed` и шорткаты `SubscribeOn*` (см. ниже). Фичевые view наследуются от `MonoView<TViewModel>` из [[UI-MVVM]].
- `ViewEvent` — internal-enum lifecycle-событий (`Open`, `Opened`, `Close`, `Closed`), который `ViewWrapper` пушит во view.
- `ViewStateNotifier` — внутренний держатель R3-стримов состояния и событий внутри `MonoView`.
- `ViewKind` — `Window`, `Popup`.
- `ViewRegistration` — опции регистрации: `EnableOnStart`, custom animator.
- `ViewFactory` — создаёт view, выбирает parent canvas и прогоняет view через `IViewSetupStep`. Ассетами не владеет: `CreateView` принимает `IAssetScope owner` и создаёт инстанс через него, поэтому инстанс view принадлежит тому же владельцу, что и ключ префаба (см. [[Assets-Addressables]]).
- `IViewSetupStep` — пост-обработка созданного view; шаг регистрируется как обычная зависимость, `ViewFactory` инжектит `IReadOnlyList<IViewSetupStep>` и остаётся feature-agnostic. Реализации в шаблоне: `CurrencyViewSetupStep` (до-инжект `CurrencyView`, `Features/Items/Scripts/View/`) и `ButtonSoundBinder` (клик-звук на кнопки view, см. [[Audio]]).
- `CanvasProvider` — создаёт `WindowCanvas` и `PopupCanvas`.
- `AutoWindow` / `AutoPopup` — атрибуты для автоматического создания и регистрации view (задают `ViewKind.Window` / `ViewKind.Popup`).

## ViewKind

- `Window` — полноэкранное окно. Создаётся в `WindowCanvas`.
- `Popup` — всплывающее окно. Создаётся в `PopupCanvas`, управляется через popup stack и background.

Стартовая активность задаётся `ViewRegistration.EnableOnStart` (дефолт — `false`), но `ViewRouter.Register`
дополнительно отключает её для `Popup`: `enableOnStart = viewKind != ViewKind.Popup && options.EnableOnStart`.
`AutoViewEntity` для `[AutoWindow]` передаёт `enableOnStart: true`, для `[AutoPopup]` — `false`, поэтому
«окно активно сразу» верно для AutoWindow; ручной `Register` popup с `EnableOnStart: true` всё равно
не активирует popup при старте.

## Как работает регистрация

`ViewRouter.Register()`:

1. Создаёт animator: `PopupAnimator` для `Popup`, `WindowAnimator` для остальных типов, если не передан custom animator.
2. Создаёт `ViewWrapper` с колбэками `view.NotifyState` / `view.NotifyEvent` — смены состояния и lifecycle-события уходят в стримы view.
3. Кладёт wrapper в dictionary по `viewKey`.
4. Выставляет стартовую активность: `viewKind != Popup && EnableOnStart`.
5. Вызывает `view.Setup(this, viewKey)`.
6. Если окно стартует активным — `WindowQueue.InitializeActive` (становится текущим только при пустой очереди; иначе pending / suspend).

`MonoView.Open()` и `MonoView.Close()` используют сохранённые `IViewRouter` и `viewKey`.

## Lifecycle-стримы view

`MonoView` публикует своё состояние и события явно (R3):

- `State` — `ReadOnlyReactiveProperty<ViewState>`, реплеит текущее значение при подписке.
- `OnOpen` — до show-анимации; `OnOpened` — после её завершения.
- `OnClose` — до hide-анимации закрытия; `OnClosed` — после её завершения.

Семантика событий (`Observable<Unit>`, без replay — поздний подписчик прошлые события не получает, текущее состояние смотри в `State.CurrentValue`):

- Suspend popup-а (swap под новым popup-ом) и постановка window в очередь **не** триггерят `OnClose`/`OnClosed`; restore из `Suspended` снова триггерит `OnOpen`/`OnOpened`.
- Закрытие уже скрытого view (не-верхний popup, pending window) — `OnClose` и `OnClosed` подряд без анимации.
- Каждое событие закрытия приходит ровно один раз на фактический переход в `Closed`.

Источник переходов — методы `ViewWrapper` (`Open` / `Close` / `Suspend` и `*Immediate`-варианты без анимации): они оборачивают вызовы animator-а и пушат события во view; каналы `PopupStack` / `WindowQueue` не мутируют состояние напрямую.

Подписка — шорткаты `MonoView.SubscribeOnOpen / SubscribeOnOpened / SubscribeOnClose / SubscribeOnClosed(Action)`: внутри уже `.AddTo(this)`, отписка происходит автоматически при уничтожении view, внешний `AddTo` не нужен. Пример — `DailyBonusCore`:

```csharp
_popupView.SubscribeOnClosed(Dispose);
_popupView.Open();
```

«Сырые» Observable-свойства нужны для композиции (`Take(1)`, `SubscribeAwait` и т.п.) — там действует общее правило `.AddTo(...)`.

Окно с `EnableOnStart` получает `OnOpen`/`OnOpened` прямо в `Register` — до того, как фичи успевают подписаться; для таких окон опираться на `State`, а не на события.

## Очередь операций и loading curtain

Все `Open`/`Close`/`CloseAll` не выполняются сразу, а кладутся операциями в единую очередь
`ViewRouter` (`ViewOperationPump`). После idle перед первым drain pump ждёт
**1 кадр** (`UniTask.NextFrame`) — достаточно, чтобы собрать запросы одного кадра
в batch, и не даёт заметного лага на интерактивных `Open` в рантайме (init-пачка и так
целиком лежит в очереди к моменту `Start()`). Затем дренит очередь по одной, `await`-я
анимацию; подряд идущие `Open` popup схлопываются в `PopupStack.OpenBatch` (см. ниже).

Pump создаётся в post-inject `[Inject]`-методе `ViewRouter`, а не в ctor: он держит `ILogChannel`,
который приходит из `ILogChannelFactory`. Post-inject проходит при резолве scope-а, задолго до
того как фичи начнут звать `Open` в своей фазе `Init`.

Исполнителем операций pump получает сам `ViewRouter` через `IViewOperationExecutor` (`this`), а не
пачку делегатов: раньше конструктор брал пять `Func`, из которых четыре всегда
приходили из одного и того же объекта. Делегатом остался только `waitFrame` — единственная
зависимость, которую тест подменяет отдельно, чтобы удерживать окно коалесинга открытым.

Pump стартует в `PostInit` фазе `ViewRouter`. Если loading curtain присутствует, его скрытие
также стартует pump (idempotent — `Start()` проверяет `_started`). Если curtain отсутствует
(debug/test-сцена), pump стартует из `PostInit` и операции выполняются нормально.

Pump обёрнут в `try/finally`: при ошибке одной операции логируется и выполняется следующая;
`_pumping` сбрасывается в `finally`. `CancellationTokenSource` отменяет все анимации в `Dispose`.

Окна с `EnableOnStart` (и `ViewKind.Window`) показываются сразу при `Register` (под curtain).
`WindowQueue.InitializeActive` делает окно текущим только если `_current == null`; иначе окно уходит
в pending / suspend текущего.

## Как работают окна

Канал `WindowQueue`: видно одно окно. `Open` второго окна, пока первое открыто, ставит его в
FIFO-очередь; при `Close` текущего показывается следующее из очереди — окна идут друг за другом.

## Как работают Popup

Канал `PopupStack`: одновременно виден ровно один popup поверх окна.

- Первый popup показывает background; background виден, пока в стеке есть хоть один popup.
- Новый popup прячет текущий (анимированно), кладёт его в стек и показывается сам.
- Временное скрытие переводит popup в `Suspended` и не триггерит `OnClose`/`OnClosed`.
- Пачка подряд идущих `Open` popup (после coalesce) — `OpenBatch`: промежуточные кладутся в стек
  как `Suspended` без Show/Hide, анимируется только верхний; background показывается один раз.
  Уже открытые и дубликаты внутри пачки (двойной клик за coalesce-окно) отфильтровываются —
  wrapper не может лечь в стек дважды.
- При закрытии верхнего popup-а верхний из стека восстанавливается полной анимацией `Show`.
- Закрытие не-верхнего popup-а просто убирает его из стека (он уже скрыт).
- `OnClosed` срабатывает ровно один раз после фактического перехода в `Closed`: после hide-анимации верхнего view или сразу для уже скрытого view.

Порядок в пачке — FIFO без приоритетов (кто раньше `Enqueue`, тот ниже в стеке).

`CloseLast()` закрывает верхний popup. Клик по popup background приходит через
`PopupBackgroundClickedSignal`.

## Как работают AutoWindow / AutoPopup

Поля с `[AutoWindow]` / `[AutoPopup]` обрабатывает source generator `AutoDecorators.Generator` (исходники — `Tools/AutoDecorators.Generator/`, собранная DLL — `Assets/Framework/Analyzers/AutoDecorators.Generator.dll` с label `RoslynAnalyzer`). Тип view определяется выбором атрибута: `AutoWindow` → `ViewKind.Window`, `AutoPopup` → `ViewKind.Popup`. На компиляции он генерит partial-часть класса с реализацией `IAutoViewHost` — массив типизированных `AutoViewBinding(viewKey, viewKind, assign)`. Класс с такими полями обязан быть `partial`, иначе генератор даёт compile error `ADG001`. Рантайм-рефлексии нет.

`AutoViewDecorator` применим к `LifecycleEntity`, реализующим `IAutoViewHost` (реализацию добавляет генератор).

Для каждого биндинга wrapper `AutoViewEntity`:

1. В `Load` создаёт собственный `IAssetScope` и грузит через него prefab.
2. В `Init` создаёт view через `ViewFactory`, передавая ему **тот же** scope владельцем инстанса.
3. Регистрирует view в `ViewRouter`.
4. Присваивает созданный view в private field базовой entity через `Assign`-делегат биндинга.
5. В `Dispose` диспозит свой scope — тот уничтожает созданные им инстансы view и отпускает свои ключи, — затем зовёт `base.Dispose()`. Если фазы были пропущены гейтом, scope не создан и `Dispose` ничего не делает.

Для `Popup` стартовая активность выключена, для `Window` включена.

Шаги 2–4 успевают до `Init` базовой entity не случайно: `SceneStarter` внутри параллельной фазы
сначала прогоняет **все** wrapper-ы и только потом **все** base entity. Это публичный контракт
lifecycle (см. [[Initialization-LifecycleEntity]], «Порядок фаз»), закреплённый тестом
`SceneStarterTests.ExecuteParallelPhase_StartsBases_AfterAllWrappersComplete`. Без него поле
с `[AutoWindow]` / `[AutoPopup]` было бы пустым в `Init` фичи.

Обратной гарантии барьер не даёт: `ViewRouter.Register`, который зовёт wrapper, не имеет права
зависеть от того, прошла ли фаза `Init` самого `ViewRouter`. Поэтому подписки роутера на
`PopupBackgroundClickedSignal` / `LoadingCurtainHiddenSignal` и `SetCanvasProvider` живут в его
post-inject (`InitRouter`), а не в `Init`. `BackgroundAnimator.SetCanvasProvider` при этом только
запоминает провайдер: сам канвас создаётся в фазе `Load`, а `Show`/`Hide` перечитывают
`BackgroundCanvasGroup` перед каждым показом.

Lifecycle-колбэков генератор не эмитит: открытие и закрытие view фича слушает явной подпиской
на стримы `MonoView` (секция «Lifecycle-стримы view»).

### Диагностики генератора

| Код | Когда | Сообщение о |
| --- | --- | --- |
| `ADG001` | класс с `[AutoWindow]` / `[AutoPopup]` / `[AutoLogger]` не `partial` | нужен `partial` |
| `ADG002` | `[AutoLogger(StatusLogs = true)]` на классе вне иерархии `LifecycleEntity` | статус-логов нет у не-entity |
| `ADG003` | два view-поля одного класса с одним ключом | дубль ключа внутри типа |
| `ADG004` | один ключ на нескольких типах в одной сборке | ключ должен быть уникален глобально |

`ADG004` закрывает коллизию ключей между разными фичами: `ViewRouter` держит один словарь на
все view, поэтому раньше такой дубль падал только в рантайме, в фазе `Init` третьей сцены.
Проверка идёт по компиляции, то есть в границах сборки; дубль между `Foundation` и `Features` ловит
EditMode-тест `AddressableKeyTests` (`Assets/Framework/Features/Tests/`).

Каждая диагностика и сам генерируемый код покрыты snapshot-тестами
`Tools/AutoDecorators.Generator.Tests/` (`powershell -File Tools/generator-tests.ps1`,
подробности — [[Testing-TDD]]). После правок генератора обязательна ещё и пересборка DLL:
`powershell -File Tools/build-generator.ps1`. Забытую пересборку ловит хэш исходников рядом
с DLL (`AutoDecorators.Generator.dll.hash`) и Stop-хук `Tools/hook-generator-hash.ps1`.

### Сверка ключей с Addressables

`AddressableKeyTests` собирает рефлексией все ключи `[AutoWindow]` / `[AutoPopup]` из сборок,
ссылающихся на `Foundation`, и сверяет их с адресами из авторинга Addressables
(`Assets/AddressableAssetsData/AssetGroups/*.asset`). Читается авторинг, а не собранный
каталог: каталога может не быть вовсе, а ошибка живёт именно в записях групп. Тесты работают и
в Unity Test Runner, и в быстром прогоне `Tools/fast-tests.ps1` — корень проекта ищется от
расположения тестовой сборки; если каталог групп не найден, тест помечается `Assert.Ignore`.

## Окно грузится только если сущность нужна

Собственного gate у `AutoViewEntity` нет и не должно быть: решение принимается **выше**, на уровне сущности, до фаз. `LifecycleGate` спрашивает два источника:

- `IsEnabled` всех инжектируемых в хост конфигов — фича выключена конфигом;
- `IConditionalEntity.ShouldRun()` — условие, которое конфигом не выразить (награда уже забрана, ивент кончился, туториал пройден).

Если решение отрицательное, `SceneStarter` не выполняет **ни одной фазы** ни для хоста, ни для его обёрток: prefab не грузится, view не создаётся, `Init` не вызывается. Со стороны фичи это один метод:

```csharp
public partial class DailyBonusCore : LifecycleEntity, IDailyBonusCore, IConditionalEntity
{
    [AutoPopup(DailyBonusConstants.Prefabs.Popup)]
    private DailyBonusPopupView _popupView;

    public bool ShouldRun()
    {
        CreateModel();
        CreateAnalytics();
        _localNow = _clock.ServerLocalNow;
        return NeedToShowPopup(_localNow);
    }
}
```

Ни ранних выходов в `Init`, ни проверок на `null`: если `Init` вызвался — view создан. Детали механики — [[Initialization-LifecycleEntity]], секция «Гейт сущности».

Так не выражаются только два случая: окно, открываемое позже по действию игрока, и условие, требующее `await`. Для них нужен ленивый хэндл — отдельная задача.

## Как расширять

Новое игровое окно или popup добавляй через [[Add-UI-Window]].

Новое поведение, общее для всех окон, добавляй в `Assets/Framework/Foundation/UI`, если оно не знает о конкретной фиче. Конкретную логику кнопок, переходов, ViewModel-ей и игровых данных оставляй в `Assets/Framework/Features/<FeatureName>`.

Если нужен особый способ анимации, сначала проверь `ViewRegistration.CustomAnimator`. Не добавляй отдельный менеджер для одного окна, пока хватает существующего `IViewAnimator`.

## Инварианты

- UI-инфраструктура живёт в `Foundation`.
- Конкретные игровые окна и popup-ы живут в `Features`.
- View-компонент должен наследоваться от `MonoView`.
- Ключ prefab-а должен совпадать с тем, что умеет загрузить активный `IAssetProvider`.
- Ключ view уникален глобально: одна и та же строка не может принадлежать двум типам.
- Для обычных игровых окон использовать `ViewKind.Window`.
- Для popup-ов использовать `ViewKind.Popup`.
- Не открывать view по строке, которая не зарегистрирована в `ViewRouter`.
- Одновременно видно одно окно и не более одного popup-а.
- `IViewAnimator.Show/Hide` возвращают `UniTask` — pump опирается на завершение анимации.
- `ViewRouter` и `AutoViewEntity` переопределяют `LifecycleEntity.Dispose()`; явная реализация `IDisposable.Dispose()` запрещена, иначе полиморфный вызов пропустит очистку UI и asset handles.

## Частые ошибки

- Создать prefab, но не добавить константу ключа.
- Добавить константу и атрибут, но забыть запись в Addressables (падает `AddressableKeyTests`).
- Скопировать ключ из другой фичи (генератор даст compile error `ADG004`).
- Добавить `[AutoWindow]` / `[AutoPopup]`, но забыть `partial` на классе (генератор даст compile error `ADG001`).
- Вызвать `Bind` до того, как `AutoViewEntity` присвоил поле.
- Гейтить загрузку view внутри фичи (проверка в `Init`, ранний выход) вместо `IConditionalEntity` — ассет всё равно загрузится.
- Закладываться на порядок `Init` между разными `LifecycleEntity`.

## Когда обновлять

Обнови эту статью, если меняются:

- контракт `IViewRouter`;
- поведение `ViewRouter.Open`, `Close`, `CloseAll`, `CloseLast`;
- правила `ViewKind`;
- процесс создания view в `ViewFactory`;
- поведение `AutoWindow` / `AutoPopup` или `AutoViewEntity`;
- набор диагностик `ADG00x` или проверки ключей в `AddressableKeyTests`;
- canvas hierarchy или parent selection.

## Last Verified

2026-08-15, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "UI-Views") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "UI-Views") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```