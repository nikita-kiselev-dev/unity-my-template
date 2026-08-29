---
title: UI MVVM
type: architecture
area: Foundation
module: UI
status: actual
source_paths:
  - Assets/Framework/Foundation/UI/Mvvm/ViewModel.cs
  - Assets/Framework/Foundation/UI/Views/MonoView.cs
  - Assets/Framework/Foundation/UI/Mvvm/BindableView.cs
  - Assets/Framework/Features/Settings/Scripts/ViewModel/SettingsViewModel.cs
  - Assets/Framework/Features/Settings/Scripts/View/SettingsPopupView.cs
  - Assets/Framework/Features/Settings/Scripts/Model/SettingsModel.cs
  - Assets/Framework/Features/Clicker/Scripts/ViewModel/ClickerViewModel.cs
  - Assets/Framework/Features/Clicker/Scripts/Model/ClickerModel.cs
  - Assets/Framework/Features/MainMenu/Scripts/ViewModel/MainMenuViewModel.cs
  - Assets/Framework/Features/DailyBonus/Scripts/ViewModel/DailyBonusViewModel.cs
related:
  - "[[UI-Views]]"
  - "[[Add-UI-Window]]"
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
tags:
  - architecture
  - foundation
  - ui
  - mvvm
  - r3
updated: 2026-08-09
---

# UI MVVM

## Для агента

Используй эту статью перед любой работой с UI-логикой фич: ViewModel, биндинги, реакции на кнопки и слайдеры, состояние окна. Это обязательный паттерн для всех новых окон и popup-ов — MVP с ручными `_view.SetX()` / `Init(callbacks)` больше не используется.

Каноничные примеры: `SettingsPopup` (two-way слайдеры), `Clicker` (команды + derived-состояние), `MainMenu` (простые команды), `DailyBonus` (список дочерних view).

## Назначение

MVVM убирает ручную синхронизацию «изменил модель — не забудь обновить view»: UI-состояние объявляется реактивными свойствами, view подписывается на них декларативно в одном месте. Компиляцию проверить нельзя без Unity, поэтому чем меньше ручных путей обновления, тем меньше рантайм-багов.

Слои и направление зависимостей (только сверху вниз):

```text
View (MonoBehaviour, пассивный)  →  ViewModel (чистый C#)  →  Model (домен, чистый C#)  →  SaveBlob/Config
Core (LifecycleEntity)  — composition root: создаёт Model и VM, зовёт view.Bind(vm), диспозит VM
```

- **Model** — домен: состояние и правила. Наружу — `ReadOnlyReactiveProperty<T>` + обычные методы. Вычисления — обычный C#; R3 только для уведомлений.
- **ViewModel** — адаптер модели под конкретный view: `ReadOnlyReactiveProperty` (состояние), `ReactiveCommand` (действия), методы для непрерывного ввода. Не знает о view и UnityEngine.UI.
- **View** — пассивный `MonoBehaviour`: serialized-ссылки + один метод `OnBind(vm)` со всеми биндингами. Никакой логики.
- **Core** (`LifecycleEntity`) — создаёт Model/VM через `new`, биндит view, владеет временем жизни VM.

## Ключевые типы

- `ViewModel` (`Foundation/UI/Mvvm`) — база VM: `IDisposable` + `protected DisposableBag Subscriptions`; все подписки и disposable-ресурсы цепляются `.AddTo(ref Subscriptions)`, `Dispose` гасит bag.
- `MonoView<TViewModel>` — база view для окон/popup-ов: наследует `MonoView` (совместим с `ViewRouter` и `[AutoWindow]` / `[AutoPopup]`), даёт `Bind(vm)` / `OnBind(vm)`. `Bind` вызывается один раз за жизнь view — подписки в `OnBind` живут через `.AddTo(this)` до `Destroy`.
- `BindableView<TViewModel>` — тот же контракт для дочерних элементов, которые не являются окном (элемент списка, ячейка). Пример — `DailyBonusDayView`.

## Правила R3

Наружу — только read-only:
- `ReactiveProperty<T>` и `Subject<T>` всегда `private`; наружу `ReadOnlyReactiveProperty<T>` / `Observable<T>`. Никогда не публиковать записываемый стрим.

Ввод пользователя:
- Дискретное действие (кнопка, клик) — `ReactiveCommand` на VM; view только вызывает `Execute`. Обработчик — подписка внутри VM. Если обработчик асинхронный или не должен выполняться повторно во время работы — `SubscribeAwait(async (_, ct) => ..., AwaitOperation.Drop)`; защита от даблкликов живёт в VM, не во view.
- Одноразовое действие (переход сцены) — `command.Take(1).Subscribe(...)`.
- Взаимоисключающие кнопки (Ok/Cancel) — `Observable.Merge` в один стрим + один `SubscribeAwait(Drop)`; `Drop` на каждой кнопке отдельно от одновременного нажатия двух кнопок не защищает.
- Непрерывный ввод (слайдер, input) — обычный метод VM (`SetSoundsVolume(float)`), не команда.

Two-way биндинг (слайдеры):
- Вниз: `vm.X.Subscribe(v => slider.SetValueWithoutNotify(v))` — `SetValueWithoutNotify` разрывает feedback loop.
- Вверх: `slider.OnValueChangedAsObservable().Subscribe(vm.SetX)`.
- Порядок обязателен: сначала VM → слайдер, потом слайдер → VM (`OnValueChangedAsObservable` реплеит текущее значение виджета при подписке).

Время жизни подписок — **любой `Subscribe()` немедленно заканчивается `.AddTo(...)`**, «голых» подписок не существует:
- Во view и любом MonoBehaviour — `.AddTo(this)`: Unity гасит подписку при уничтожении объекта.
- В VM — `.AddTo(ref Subscriptions)` (bag — struct, только через `ref`); VM владеет своей моделью и командами: `model.AddTo(ref Subscriptions)`, `command.AddTo(ref Subscriptions)`.
- В прочих не-Mono классах — `DisposableBag _subscriptions` + `.AddTo(ref _subscriptions)` + `Dispose` (то же правило, что для сигналов в `AGENTS.md`).
- Core диспозит VM в `Dispose()` (и в teardown-путях, если фича сворачивается раньше).
- `Bind` у view вызывается один раз за жизнь инстанса: подписки живут до `Destroy`, повторный `Bind` их задублирует.

Границы применения R3:
- Домен и вычисления — обычный C# (`if`, LINQ/ZLinq); R3 только для уведомлений и композиции потоков во времени.
- Не использовать `Observable.EveryUpdate()` как замену `Update()`; per-frame стрим допустим только для композиции (`Where(...).Take(1)` и т.п.).
- Таймеры — всегда с явным провайдером: в классах под DI — инжектируемый `TimeProvider` (зарегистрирован в `RootScope`, в плеере это Unity-провайдер R3; в тестах — `FakeTimeProvider`), во view-коде — `UnityTimeProvider` / `UnityFrameProvider`.
- Ошибки в подписках не глотать: логировать через `ILogChannel`, `OperationCanceledException` — исключение.
- Не выносить длинные цепочки операторов: если цепочка не читается — разбить на именованные промежуточные Observable или обычный метод.

## Эталонный пример (SettingsPopup)

```csharp
// Model: домен, наружу read-only стримы + методы
public class SettingsModel : IDisposable
{
    private readonly ReactiveProperty<float> _soundsVolume;
    public ReadOnlyReactiveProperty<float> SoundsVolume => _soundsVolume;

    public void SetSoundsVolume(float volume)
    {
        _data.SetSoundsVolumeData(volume);
        _soundsVolume.Value = _data.SoundsVolume;
    }
}

// ViewModel: адаптер + side effects декларативно
public class SettingsViewModel : Framework.Foundation.UI.Mvvm.ViewModel
{
    public ReadOnlyReactiveProperty<float> SoundsVolume => _model.SoundsVolume;

    public SettingsViewModel(SettingsModel model, IAudioController audioController)
    {
        _model = model;
        _model.AddTo(ref Subscriptions);
        _model.SoundsVolume.Subscribe(audioController.SetSoundsVolume).AddTo(ref Subscriptions);
    }

    public void SetSoundsVolume(float volume) => _model.SetSoundsVolume(volume);
}

// View: все биндинги в OnBind, каждый Subscribe заканчивается .AddTo(this)
public sealed class SettingsPopupView : MonoView<SettingsViewModel>
{
    protected override void OnBind(SettingsViewModel viewModel)
    {
        viewModel.SoundsVolume.Subscribe(v => m_SoundsVolumeSlider.SetValueWithoutNotify(v)).AddTo(this);
        m_SoundsVolumeSlider.OnValueChangedAsObservable().Subscribe(viewModel.SetSoundsVolume).AddTo(this);
        m_CloseButton.OnClickAsObservable().Subscribe(_ => Close()).AddTo(this);
    }
}

// Core: composition root
protected override UniTask Init()
{
    _viewModel = new SettingsViewModel(new SettingsModel(_data), _audioController);
    _view.Bind(_viewModel);
    ...
}

public override void Dispose()
{
    _viewModel?.Dispose();
    base.Dispose();
}
```

Команды и derived-состояние — см. `Clicker`: `ReactiveCommand Upgrade`, `CanUpgrade` считается в `ClickerModel` (`_level.Select(...).ToReadOnlyReactiveProperty()`), VM только пробрасывает, во view — `viewModel.CanUpgrade.SubscribeToInteractable(m_UpgradeButton).AddTo(this)`.

Список дочерних элементов — см. `DailyBonus`: popup-VM держит `IReadOnlyList<DailyBonusDayViewModel>`, дочерние view (`BindableView`) создаёт `DailyBonusDayViewSpawner` и биндит к своим VM по одному.

## Как добавить

Полный рецепт нового окна — [[Add-UI-Window]]. Кратко: `Model` (если есть домен) → `ViewModel` → `View : MonoView<TVM>` → в `Core.Init` создать VM, `_view.Bind(vm)`, в `Dispose` — `_viewModel?.Dispose()`.

## Стиль и именование

- Роль называется только `ViewModel` (папка `Scripts/ViewModel/`); имена `Presenter` / `Controller` для UI-логики не используются.
- Для фиче-внутренних Model/ViewModel интерфейсы `I*` не заводятся: они создаются через `new` рядом с использованием, второй реализации нет. Интерфейсы — только на границах фич (`ISettingsCore`) и для инфраструктуры.
- Базовый класс указывается полностью: `Framework.Foundation.UI.Mvvm.ViewModel` — сегмент namespace `ViewModel` затеняет короткое имя (тот же приём, что с `Framework.Foundation.SaveLoad.SaveBlob`).

## Инварианты

- View не читает и не пишет Model/Data напрямую — только через VM.
- VM не ссылается на view и UnityEngine.UI; допустимы platform-типы данных (`Sprite`, `Transform`).
- Каждый `Subscribe()` заканчивается `.AddTo(...)`: во view/MonoBehaviour — `.AddTo(this)`, в VM — `.AddTo(ref Subscriptions)`; «голых» подписок без владельца не существует.
- `Bind` у view вызывается один раз за жизнь инстанса.
- Каждый созданный VM кто-то диспозит (обычно Core).
- `ReactiveProperty` / `Subject` не выходят за пределы класса-владельца.
- Наследники `MonoView<TVM>` совместимы со всей инфраструктурой [[UI-Views]] (`[AutoWindow]` / `[AutoPopup]`, `ViewFactory`, `IViewSetupStep`) без изменений.

## Частые ошибки

- `Subscribe()` без `.AddTo(...)` — утечка подписки; это запрещено без исключений.
- `.AddTo(ref Subscriptions)` без `ref` не скомпилируется, но копия bag-а в локальную переменную молча теряет подписки — bag это struct, работать только через `ref`.
- Повторный `Bind` того же view — дублирование подписок (`.AddTo(this)` живёт до `Destroy`).
- Обратный порядок two-way биндинга слайдера — значение виджета из префаба затирает сохранённое.
- `slider.value = x` вместо `SetValueWithoutNotify` — feedback loop через `onValueChanged`.
- Забыть `_viewModel?.Dispose()` в Core — живые подписки на модель после смерти сцены.
- Логика принятия решений в операторах R3 вместо обычного метода — нечитаемо и неотлаживаемо.
- Повторное использование задиспоженного `DisposableBag` без `= default` — новые подписки умирают мгновенно.

## Когда обновлять

Обнови эту статью, если меняются:

- контракт `ViewModel` / `MonoView<TVM>` / `BindableView<TVM>`;
- правила владения подписками или порядок two-way биндинга;
- каноничный пример перестаёт быть `SettingsPopup` / `Clicker`;
- появляется новый вид биндинга (списки с изменяемым составом, анимации от VM и т.п.).

## Last Verified

2026-08-09, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "UI-MVVM") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "UI-MVVM") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```