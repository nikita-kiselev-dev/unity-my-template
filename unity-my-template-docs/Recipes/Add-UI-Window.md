---
title: Add UI Window
type: recipe
area: Features
module: UI
status: actual
source_paths:
  - Assets/Framework/Features/MainMenu/Scripts/MainMenuCore.cs
  - Assets/Framework/Features/MainMenu/Scripts/View/MainMenuWindowView.cs
  - Assets/Framework/Features/MainMenu/Scripts/ViewModel/MainMenuViewModel.cs
  - Assets/Framework/Features/MainMenu/Scripts/MainMenuConstants.cs
  - Assets/Framework/Features/Settings/Scripts/SettingsCore.cs
  - Assets/Framework/Features/Settings/Scripts/View/SettingsPopupView.cs
  - Assets/Framework/Features/Settings/Scripts/ViewModel/SettingsViewModel.cs
  - Assets/Framework/Features/Settings/Scripts/SettingsConstants.cs
  - Assets/Framework/Foundation/UI/Mvvm/
  - Assets/Framework/Foundation/UI/Views/
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[UI-Views]]"
  - "[[UI-MVVM]]"
  - "[[Testing-TDD]]"
tags:
  - recipe
  - features
  - ui
  - window
  - popup
  - auto-view
  - mvvm
updated: 2026-07-26
---

# Add UI Window

## Для агента

Используй этот рецепт, если нужно добавить новое игровое окно или popup. По умолчанию следуй паттерну `MainMenu` для окна и `SettingsPopup` для popup-а.

UI-логика пишется по MVVM ([[UI-MVVM]] — обязательно к прочтению): view пассивный и биндится к ViewModel, ручных `Init(callbacks)` / `SetX()`-проводок нет.

Новая игровая UI-фича живёт в `Assets/Framework/Features/<FeatureName>/`, использует `MonoView<TViewModel>`, `partial LifecycleEntity`, `[AutoWindow]` / `[AutoPopup]`, `[AutoRegistration]` и `[LifecycleOrder]`.

## Короткий чеклист

1. Создать папку `Assets/Framework/Features/<FeatureName>/`.
2. Добавить `Content/` для prefab-а.
3. Добавить `Scripts/` для feature core, `View/`, `ViewModel/`, constants и интерфейсов.
4. Создать constants-класс с ключом prefab-а.
5. Написать красные тесты на Model/ViewModel в `Assets/Framework/Features/Tests/` ([[Testing-TDD]]) — до реализации.
6. Создать `<FeatureName>ViewModel : Framework.Foundation.UI.Mvvm.ViewModel` — команды, read-only состояние; при наличии домена — отдельный `Model/`; довести тесты до зелёного (`Tools/fast-tests.ps1`).
7. Создать view-компонент от `MonoView<<FeatureName>ViewModel>` с биндингами в `OnBind`.
8. Создать `partial <FeatureName>Core : LifecycleEntity` (partial обязателен — реализацию `IAutoViewHost` генерит AutoDecorators.Generator).
9. Добавить `[AutoRegistration]`, `[LifecycleOrder]`.
10. Добавить private field с `[AutoWindow(...)]` (окно) или `[AutoPopup(...)]` (popup).
11. В `Init` создать VM и вызвать `_view.Bind(_viewModel)`; в `Dispose` — `_viewModel?.Dispose()`.
12. Если окно нужно не всегда — конфиг с `IsEnabled` или `IConditionalEntity` (секция «Окно нужно не всегда»).
13. Проверить prefab в Unity Editor.

## Рекомендуемая структура

```text
Assets/Framework/Features/<FeatureName>/
  Content/
    <FeatureName>.prefab
  Scripts/
    <FeatureName>Constants.cs
    <FeatureName>Core.cs
    I<FeatureName>Core.cs
    View/
      <FeatureName>View.cs
    ViewModel/
      <FeatureName>ViewModel.cs
```

Если у фичи есть домен или сохраняемое состояние, добавь `Model/`, `Data/`; при необходимости — `Configs/`, `Signals/`, `Factory/`.

## Constants

Ключ prefab-а хранится рядом с фичей:

```csharp
namespace Framework.Features.SomeFeature
{
    public static class SomeFeatureConstants
    {
        public static class Prefabs
        {
            public const string Window = "SomeFeature";
        }
    }
}
```

Для popup-а используй имя `Popup`:

```csharp
public const string Popup = "SomePopup";
```

## ViewModel

ViewModel — чистый C#: команды для дискретных действий, `ReadOnlyReactiveProperty` для состояния, обычные методы для непрерывного ввода. Каждая подписка и disposable-ресурс — `.AddTo(ref Subscriptions)` (bag — struct, только через `ref`).

```csharp
public class SomeFeatureViewModel : Framework.Foundation.UI.Mvvm.ViewModel
{
    public ReactiveCommand Confirm { get; } = new();

    public SomeFeatureViewModel(ISomeService someService)
    {
        Confirm.AddTo(ref Subscriptions);
        Confirm.Subscribe(_ => someService.Confirm()).AddTo(ref Subscriptions);
    }
}
```

Базовый класс указывается полностью (`Framework.Foundation.UI.Mvvm.ViewModel`) — сегмент namespace `ViewModel` затеняет короткое имя.

## View

View наследуется от `MonoView<TViewModel>` и содержит только serialized fields и биндинги в `OnBind` — каждый `Subscribe()` заканчивается `.AddTo(this)`.

```csharp
public sealed class SomeFeatureView : MonoView<SomeFeatureViewModel>
{
    [SerializeField] private Button m_ConfirmButton;
    [SerializeField] private Button m_CloseButton;

    protected override void OnBind(SomeFeatureViewModel viewModel)
    {
        m_ConfirmButton.OnClickAsObservable().Subscribe(viewModel.Confirm.Execute).AddTo(this);
        m_CloseButton.OnClickAsObservable().Subscribe(_ => Close()).AddTo(this);
    }
}
```

Не размещай бизнес-логику во view. Закрытие собственного view кнопкой — единственное, что view делает сам (`Close()` из `MonoView`). Правила биндингов (two-way, интерактивность, Drop) — в [[UI-MVVM]].

## Feature Core

Окно:

```csharp
[AutoRegistration]
[LifecycleOrder(SceneConstants.Scenes.Start, (int)StartSceneInitOrder.SomeFeature)]
public partial class SomeFeatureCore : LifecycleEntity, ISomeFeatureCore
{
    [AutoWindow(SomeFeatureConstants.Prefabs.Window)]
    private SomeFeatureView _view;

    private SomeFeatureViewModel _viewModel;

    protected override UniTask Init()
    {
        _viewModel = new SomeFeatureViewModel(...);
        _view.Bind(_viewModel);
        SetEnabled(true);
        // IsInited выставит InitPhase после успешного Init — вручную SetInited() не вызывать.
        return UniTask.CompletedTask;
    }

    public override void Dispose()
    {
        _viewModel?.Dispose();
        base.Dispose();
    }
}
```

Popup:

```csharp
[AutoPopup(SomeFeatureConstants.Prefabs.Popup)]
private SomeFeatureView _view;
```

`ViewKind` определяется выбором атрибута: `[AutoWindow]` → `Window`, `[AutoPopup]` → `Popup`.

Для popup-а внешний API обычно выглядит как `OpenPopup()` на интерфейсе feature core, а само открытие делается через `_view.Open()`.

## Окно нужно не всегда

Если фича выключается конфигом — ничего писать не надо: заведи конфиг с `IsEnabled`, и `SceneStarter` не выполнит ни одной фазы, окно не загрузится.

Если условие конфигом не выражается (ивент кончился, награда уже забрана, туториал пройден) — реализуй `IConditionalEntity`:

```csharp
public partial class SomeFeatureCore : LifecycleEntity, IConditionalEntity
{
    public bool ShouldRun()
    {
        return /* синхронное решение: конфиг, сейв, IClock уже готовы */;
    }
}
```

`ShouldRun` вызывается один раз до фаз. Вернул `false` — ни одна фаза не выполняется, prefab не грузится, `Init` не вызывается. Вернул `true` — всё работает как обычно: ранние выходы и проверки на `null` внутри `Init` не нужны.

Подробности и ограничения — [[Initialization-LifecycleEntity]], секция «Гейт сущности». Живой пример — `DailyBonusCore`.

## InitOrder

Добавь значение в соответствующий enum из `Assets/Framework/Foundation/Initialization/Scripts/InitOrder/`.

Выбирай порядок по реальной зависимости:

- если фича зависит от canvas/view infrastructure, она должна идти после `CanvasProvider` и `ViewRouter`;
- если зависимость не требует порядка внутри `Init`, не закладывайся на порядок, потому что `Init` сейчас параллельный;
- если нужна строгая последовательность после `Init` другой entity, используй `PostInit` или явный status/сигнал.

## Prefab

Prefab должен:

- лежать там, откуда его загрузит активный `IAssetProvider`;
- иметь компонент view-наследника `MonoView<TViewModel>`;
- иметь заполненные serialized fields;
- соответствовать ключу из constants;
- использовать подходящий canvas через `ViewKind`, а не вручную выбирать parent.

## Инварианты

- Игровое окно или popup размещается в `Assets/Framework/Features/`.
- UI-инфраструктура не копируется в фичу.
- Feature core наследуется от `LifecycleEntity`, если нужен scene lifecycle.
- `partial` на классе обязателен, если используются поля `[AutoWindow]` / `[AutoPopup]` или атрибут `[AutoLogger]` (иначе compile error `ADG001`).
- UI-логика — по MVVM: view пассивный; каждый `Subscribe()` заканчивается `.AddTo(this)` во view / `.AddTo(ref Subscriptions)` в VM (см. [[UI-MVVM]]).
- Роль UI-логики называется `ViewModel`; имена `Presenter` / `Controller` не используются.
- View не должна напрямую управлять scene state, save/load или DI.
- Не использовать `Debug.Log`; логи идут через `ILogChannel`.
- В feature core логгер приходит через `[AutoLogger(<Feature>Constants.LogName, LogCategory.Feature, StatusLogs = true)]` на классе, обращение — через `Logger`; в модели, создаваемые через `new`, `ILogChannel` пробрасывается параметром. `[Inject] ILogChannelFactory` + ручной `Get(...)` — исключение для классов, которым логгер нужен в ctor или в собственном `[Inject]`-методе (двух `[Inject]`-методов на типе быть не должно).

## Частые ошибки

- Не добавить `[LifecycleOrder]` для нужной сцены.
- Не добавить `[AutoRegistration]`, из-за чего entity не попадёт в контейнер.
- Назвать prefab key иначе, чем asset key.
- Использовать `ViewKind.Window` для popup-а.
- Ожидать, что `Init` другой entity уже завершился.
- Забыть `_viewModel?.Dispose()` в Core.
- Гейтить показ окна проверкой внутри `Init` — prefab всё равно загрузится; условие место в `IConditionalEntity`.
- Добавить `.csproj`/`.sln` изменения после Unity regeneration.

## Когда обновлять

Обнови этот рецепт, если меняются:

- паттерн `AutoWindow` / `AutoPopup`, `IConditionalEntity` или MVVM-база (`ViewModel`, `MonoView<TViewModel>`);
- структура игровых UI-фич;
- способ регистрации `LifecycleEntity`;
- правила `ViewKind`;
- требования к prefab-ам;
- canonical example перестаёт быть `MainMenu` или `SettingsPopup`.

## Last Verified

2026-07-26, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Add-UI-Window") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Add-UI-Window") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```