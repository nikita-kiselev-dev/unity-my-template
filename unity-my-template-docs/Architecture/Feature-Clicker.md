---
title: Feature-Clicker
type: architecture
area: Features
module: Clicker
status: actual
source_paths:
  - Assets/Framework/Features/Clicker/
related:
  - "[[UI-MVVM]]"
  - "[[Feature-Items]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Configs]]"
tags:
  - architecture
  - clicker
  - features
  - mvvm
updated: 2026-08-08
---

# Feature-Clicker

## Для агента

Открывай эту статью в двух случаях: трогаешь кликер или пишешь **новую фичу** и хочешь эталон.

Кликер — самая полная фича шаблона: у неё есть все шесть слоёв (Core, Model, ViewModel, View, Data,
Config), аналитика и тесты. Ни одного слоя «на будущее» в ней нет, поэтому её структуру можно
копировать целиком.

Правила MVVM, которым она следует, живут в [[UI-MVVM]]; здесь — как они выглядят на живой фиче и
какие решения приняты именно здесь.

## Стек фичи

| Файл | Слой | Роль |
| --- | --- | --- |
| `ClickerCore` | composition root | `LifecycleEntity` на `Meta`, создаёт модель/VM, биндит view |
| `ClickerModel` | домен | уровень, доход, апгрейд; чистый C#, без Unity |
| `ClickerViewModel` | VM | две команды, проброс `CanUpgrade`, вызов аналитики |
| `ClickerWindowView` | view | `MonoView<ClickerViewModel>`, только биндинги |
| `ClickAreaView` | view | зона тапа, отдаёт `Observable<Unit>` |
| `ClickerData` | сейв | счётчик кликов и уровень |
| `ClickerConfig` / `ClickerLevelConfig` | конфиг | список уровней: доход, цена, tier |
| `ClickerAnalytics` / `IClickerAnalytics` | аналитика | событие апгрейда |
| `IClickerCore` | граница фичи | пустой интерфейс поверх `IEntityStatus` |

`IClickerCore` пуст, и это законно: пустота и есть контракт — «снаружи про кликер можно узнать
только статус». Такой интерфейс допустим как граница фичи, см. [[Naming]].

## Core: как собирается фича

```csharp
[AutoRegistration]
[LifecycleOrder(SceneConstants.Scenes.Meta, (int)MetaSceneInitOrder.Clicker)]
[AutoLogger(ClickerConstants.LogName, LogCategory.Feature, StatusLogs = true)]
public partial class ClickerCore : LifecycleEntity, IClickerCore
{
    [AutoWindow(ClickerConstants.Prefabs.Window)]
    private ClickerWindowView _windowView;
```

Четыре атрибута закрывают весь boilerplate: регистрация в контейнере, порядок инициализации на
сцене, логгер со статус-логами и загрузка/создание/регистрация окна. Класс обязан быть `partial` —
`[AutoWindow]` и `[AutoLogger]` обрабатывает source generator, см. [[UI-Views]].

`Init` делает ровно четыре вещи и ничего больше (`ClickerCore.cs:36-43`): создаёт модель, создаёт
аналитику, биндит VM во view, зовёт `SetActive()`. Никаких проверок на `null`, никаких ранних
выходов: если конфиг выключен (`is_enabled: false`), `LifecycleGate` погасит сущность **до** фаз, и
`Init` вообще не вызовется — см. [[Initialization-LifecycleEntity]].

Владение — цепочкой: `Dispose` диспозит только VM, а та в своём bag держит модель и команды
(`ClickerViewModel.cs:20-24`). `?.` в `_viewModel?.Dispose()` стоит не «на всякий случай»: у
выключенной фичи `Init` не отработал, и VM действительно нет.

## Model: где живёт вся логика

`ClickerModel` — обычный C#-класс без единого `using UnityEngine`. Наружу отдаёт три
`ReadOnlyReactiveProperty`: `Level`, `CurrentLevelConfig`, `CanUpgrade`; внутри `_level` —
`ReactiveProperty`, и два производных свойства вычисляются из него через `Select`
(`ClickerModel.cs:32-34`). Это и есть правило «домен считает обычным C#, R3 только для
уведомлений»: пересчёт не подписан на таймер и не крутится в `Update`.

Два решения, ради которых стоит читать этот файл:

**`ClampLevel` — защита от расхождения сейва и конфига** (`ClickerModel.cs:39-49`). Уровни могут
урезать патчем после того, как игрок докачался, или сейв может оказаться битым. Обращение к
`config.Levels[level]` по такому индексу — падение фичи на старте, поэтому уровень зажимается в
диапазон конфига и пишется `LogError`. Это ровно тот случай, когда проверка **нужна**: расхождение
данных с конфигом — реальный сценарий, а не «на всякий случай» (см. [[Utilities]]).

**Guard хот-пасса в `Click`** (`ClickerModel.cs:56-63`). Клик происходит на каждое нажатие, а
`ToString` + `SetFeatureColor` + интерполяция считаются на стороне вызывающего — без guard-а мусор
копился бы даже с выключенными логами. `TryUpgrade` guard-а не имеет и не должен: апгрейд редкий.

Порядок в `TryUpgrade` (`ClickerModel.cs:66-83`): проверить возможность → **списать** → повысить
уровень. Списание раньше изменения состояния — потому что `IInventory.Remove` может вернуть `false`,
и откатывать уже применённый уровень было бы нечем (см. [[Feature-Items]]).

## ViewModel и View

VM тонкая по замыслу: две `ReactiveCommand`, проброс `CanUpgrade` из модели и один метод с логикой
(`OnUpgrade`) — аналитика шлётся **только на успешный** апгрейд (`ClickerViewModel.cs:27-33`).
Уровень в событие уходит как `_model.Level.CurrentValue + 1`, потому что аналитике нужен номер
уровня, а не индекс.

View не знает ни модели, ни данных (`ClickerWindowView.cs:17-22`):

```csharp
m_ClickArea.Clicked.Subscribe(viewModel.Click.Execute).AddTo(this);
m_UpgradeButton.OnClickAsObservable().Subscribe(viewModel.Upgrade.Execute).AddTo(this);
viewModel.CanUpgrade.SubscribeToInteractable(m_UpgradeButton).AddTo(this);
```

`ClickAreaView` — отдельный компонент, а не обработчик в окне: тапать нужно по конкретной области, и
`IPointerDownHandler` требует своего `MonoBehaviour`. Он отдаёт наружу `Observable<Unit>`, а `Subject`
держит приватным и диспозит в `OnDestroy` — то же правило, что для VM.

`[CurrencyViewHost]` на классе окна включает инжект вложенных `CurrencyView`: баланс в окне работает
без единой строки кода в кликере, см. [[Feature-Items]].

## Конфиг и данные

`ClickerConfig` — список уровней; каждый уровень несёт доход за клик, цену апгрейда и `ClickerTier`
(визуальный тир). Числа в JSON — `long`, наружу отдаются как `BigInteger`
(`ClickerLevelConfig.cs:11-17`): конфиг остаётся человекочитаемым, а арифметика идёт в том же типе,
что и баланс.

`ClickerData` хранит `ClickCount` и `Level` с приватными сеттерами и меняется только своими методами
`OnClick`/`Upgrade`. Новые сериализуемые члены добавляются **только в конец** объявления — MemoryPack
работает в дефолтном режиме, см. [[SaveLoad]].

## Инварианты

- `ClickerModel` не ссылается на Unity: в файле нет `using UnityEngine`.
- View не трогает `ClickerModel` и `ClickerData` — только `ClickerViewModel`.
- Наружу из модели уходят `ReadOnlyReactiveProperty`; `ReactiveProperty` приватный.
- Каждый `Subscribe` заканчивается `.AddTo(...)`: во view — `.AddTo(this)`, в VM —
  `.AddTo(ref Subscriptions)`.
- Уровень из сейва всегда зажимается в диапазон конфига перед обращением к `Levels[...]`.
- `Click` логируется под guard `AreLogsEnabled` с форматированием внутри guard-а.
- Аналитика апгрейда отправляется только при `TryUpgrade() == true`.
- `ClickerCore` объявлен `partial` и не содержит ручного поля логгера или ручной загрузки префаба.
- Ключ `ClickerConstants.Prefabs.Window` есть в Addressables (`AddressableKeyTests`).

## Как расширять

**Новый уровень** — запись в `ClickerConfig.json`. Кода менять не нужно: `CanUpgrade` считается от
длины массива.

**Автокликер / доход в оффлайне**: время берётся через `IClock` (см. [[Time]]), состояние — в
`ClickerData` новым членом **в конец**. Тик — не `Observable.EveryUpdate()`, а таймер с явным
провайдером; в модели это инжектируемый `TimeProvider`, см. [[UI-MVVM]].

**Множители и бусты**: расчёт дохода уходит из `Click` в отдельный метод модели, чтобы бусты не
размазались по вызывающим. `IncomePerClick` остаётся базой из конфига.

**Визуал по тиру.** `ClickerTier` сейчас читается только из конфига и никем не используется — это
заготовка под смену внешнего вида кликера по уровню. Подключается во view подпиской на
`CurrentLevelConfig`, а не новым полем в модели.

**Вторая валюта за клик**: `IInventory.Add` вызывается с явным ключом
(`new ItemOperation(key, value)`); менять `ItemOperation` не нужно.

## Тесты

`Assets/Framework/Features/Tests/`:

- `ClickerModelTests` — 11 тестов: начальное состояние, clamp уровня при сейве впереди конфига,
  доход за клик, счётчик кликов, guard логов (включён/выключен), списание при апгрейде, отказ на
  максимальном уровне и при нехватке валюты, переход `CanUpgrade` в `false`.
- `ClickerViewModelTests` — команды и отправка аналитики.

Фейки: `FakeInventory`, `FakeClickerAnalytics`, `FakeLogChannel`. Конфиг строится из JSON через
Newtonsoft (`FeaturesTestConfigs`) — тем же путём, что в проде.

`ClickerCore` не тестируется: composition root проверять нечем, вся логика вынесена в модель. Если
захотелось протестировать Core — логика утекла не в тот слой, см. [[Testing-TDD]].

## Когда обновлять

- Изменился состав слоёв фичи или порядок вызовов в `ClickerCore.Init`.
- В `ClickerModel` появилось новое состояние или изменилось правило `ClampLevel`.
- В `ClickerData` добавлен член (и это должно быть отражено в [[SaveLoad]]).
- Изменилась схема `ClickerConfig.json`.
- `ClickerTier` получил потребителя.
- Фича перестала быть эталоном MVVM — тогда эталон надо назначить явно и переписать эту статью.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Feature-Clicker") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Feature-Clicker") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
