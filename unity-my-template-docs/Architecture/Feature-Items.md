---
title: Feature-Items
type: architecture
area: Features
module: Items
status: actual
source_paths:
  - Assets/Framework/Features/Items/
related:
  - "[[Foundation-vs-Features]]"
  - "[[SaveLoad]]"
  - "[[UI-Views]]"
  - "[[Configs]]"
tags:
  - architecture
  - items
  - features
  - economy
updated: 2026-08-08
---

# Feature-Items

## Для агента

Открывай эту статью, когда трогаешь экономику: валюты, счётчики, выдачу и списание, отображение
баланса.

Единственная точка входа для другой фичи — `IInventory`:

```csharp
[Inject] private readonly IInventory _inventory;

if (_inventory.IsEnough(new ItemOperation(price)))
{
    _inventory.Remove(new ItemOperation(price));
}
```

Три вещи, которые экономят время:

- **Все методы возвращают `bool` и никогда не бросают.** `false` — операция не применена (нет такой
  валюты, не хватает, значение `<= 0`). Проверять `null` не нужно.
- **`ItemOperation` без ключа означает основную валюту** (`ItemsConstants.MainCurrencyKey` —
  `"dollar"`), а без значения — единицу. `new ItemOperation()` = «одна главная валюта».
- **Баланс не читается синхронным геттером.** Наружу отдаётся `ReadOnlyReactiveProperty<BigInteger>`
  через `TryGetCounter(key, out var counter)` → `counter.Info.Value`.

## Почему это `Features`, а не `Foundation`

Экономика — про конкретную игру: набор валют, их имена, иконки, главная валюта. Переиспользуемого
здесь нет, поэтому вся фича живёт в `Features/Items/` — вместе с `CurrencyView`, который её
показывает. Правило границы — [[Foundation-vs-Features]].

`Foundation` при этом даёт всё, на чём фича стоит: `IIconProvider` (атласы иконок), локализация,
`SaveBlob`, `IConfig`, `IViewSetupStep`.

## Слои

| Слой | Тип | Что делает |
| --- | --- | --- |
| Composition root | `Inventory` | `LifecycleEntity` на Bootstrap, создаёт счётчики в `Init` |
| Домен | `ItemCounter` | реактивная обёртка над одним ключом |
| Данные | `ItemsData` | `SaveBlob`, словарь `ключ → BigInteger` |
| Снимок | `ItemInfo` | ключ, имя, описание, иконка, значение |
| Аргумент | `ItemOperation` | пара «ключ + количество» |
| Конфиг | `CurrenciesConfig` | список валют |
| View | `CurrencyView` | показ иконки и значения одной валюты |

`Inventory` — Singleton, зарегистрирован автоматически и объявлен на сцене `Bootstrap`
(`Inventory.cs:15-18`). Он должен быть готов раньше любой игровой сцены: счётчики создаются один раз
за процесс и переживают смену сцен.

## Реактивность и данные

Разделение жёсткое и продиктовано форматом сейва (см. [[SaveLoad]]):

- `ItemsData` хранит **только простые значения** — `Dictionary<string, BigInteger>`. `ReactiveProperty`
  и прочие типы R3 в сохраняемую схему не попадают: иначе формат сейва зависел бы от версии пакета.
  Инвариант закрыт тестом `ItemsDataTests.Items_StorePlainValues_WithoutReactiveWrappers`.
- `ItemCounter` держит `ReactiveProperty<BigInteger>`, пишет в данные и отдаёт наружу
  `ReadOnlyReactiveProperty` (`ItemCounter.cs:11-25`).

Порядок записи в `ItemCounter.Add`/`Remove` — сначала данные, потом синхронизация свойства
(`ItemCounter.cs:27-52`). Если данные операцию отклонили, свойство не трогается и подписчики не
дёргаются: источник истины — `ItemsData`, а не реактивная обёртка.

Правила самих данных (`ItemsData.cs:37-64`):

- количество `<= 0` отклоняется и для `Add`, и для `Remove` — «добавить минус пять» не должно
  работать как списание;
- списание в минус отклоняется, до нуля — разрешено;
- неизвестный ключ отклоняется, а не создаёт запись: набор валют задаёт конфиг, а не вызывающий.

## Создание счётчиков

`Inventory.Init` создаёт `ItemCounterFactory` и просит `CreateAll` (`Inventory.cs:84-88`). Фабрика
проходит по `CurrenciesConfig.Currencies` и для каждой валюты (`ItemCounterFactory.cs:28-48`):

1. локализует имя и описание по форматам `{0}_name` / `{0}_description` из таблицы `currencies`;
2. берёт иконку из атласа через `IIconProvider` по формату `{0}_icon`;
3. заводит запись в `ItemsData` (`AddNewItem` не сбрасывает существующее значение);
4. создаёт `ItemCounter`.

Отсюда конвенция имён ассетов валюты: id валюты — это **ключ словаря в сейве**, и все производные
имена собираются форматом из него. Для `gem` это `gem_name`, `gem_description`, `gem_icon`,
`gem_icon_atlas`. Такие ключи наследуют стиль данных (`snake_case`), а не PascalCase-конвенцию
Addressables — это правило, а не исключение, см. [[Naming]].

Практическое следствие: **смена регистра или написания id валюты стоит миграции сейва**. Ключ лежит
в словаре `ItemsData`, и переименование без миграции обнулит баланс игрока.

Ассеты валюты лежат по папке на валюту: `Items/Dollar/Content/`, `Items/Gems/Content/`,
`Items/Gold/Content/`; общее (конфиг, префабы `CurrencyView`, общий атлас) — в `Items/Common/Content/`.

## CurrencyView

`CurrencyView` — обычный `MonoBehaviour` (не `MonoView`, не `BindableView`): он не привязан к
ViewModel и живёт своей жизнью внутри любого окна. В `Start` он ищет счётчик по сериализованному
ключу `m_Key` и либо подписывается на значение, либо гасит себя и пишет `LogError`
(`CurrencyView.cs:22-43`).

Два решения стоит помнить:

- **Схлопывание по кадру.** Подписка идёт через `.ThrottleLastFrame(1, UnityFrameProvider.Update)`:
  значение меняется пачками (серия тапов за кадр), а увидеть игрок может только последнее. Без
  схлопывания каждый промежуточный тап платил бы за `BigInteger.ToString` — 280 B на 41-значное
  число (замер в `ItemCounterAllocationTests`).
- **Инжект через `IViewSetupStep`.** `CurrencyView` лежит внутри чужого префаба, и VContainer сам
  до него не доберётся. `CurrencyViewSetupStep` вызывает `ChildComponentInjector.Inject` для view,
  помеченных `[CurrencyViewHost]` (`CurrencyViewSetupStep.cs:15`). Это фича-специфичный шаг,
  зарегистрированный в `Features`, — `ViewFactory` остаётся feature-agnostic, см. [[UI-Views]].

Отсюда правило для нового окна с балансом: положить префаб `CurrencyView` внутрь, выставить `m_Key`
в Inspector и повесить `[CurrencyViewHost]` на класс view. Кода не нужно.

## Инварианты

- Изменения количества проходят **только** через `IInventory`: члены `ItemsData` объявлены
  `internal`, и `grep -rn "ItemsData" Assets/Framework --include=*.cs` вне `Features/Items/` и тестов
  даёт только регистрацию save-тега (`FeaturesSaveTags.cs:14`) и комментарий про освободившийся
  номер в `FoundationSaveTags.cs`.
- `ItemsData` не содержит типов R3 (`ItemsDataTests.Items_StorePlainValues_WithoutReactiveWrappers`).
- Новые сериализуемые члены `ItemsData` добавляются только в конец объявления (MemoryPack в
  дефолтном режиме), см. [[SaveLoad]].
- `Add`/`Remove` с количеством `<= 0` возвращают `false` и не меняют данные.
- Списание ниже нуля возвращает `false`; списание ровно в ноль разрешено.
- Наружу из `ItemCounter` уходит `ReadOnlyReactiveProperty`, не `ReactiveProperty`
  (`ItemsDataTests.ItemConfig_Value_ExposesReadOnlyReactiveProperty`).
- Каждой валюте из `CurrenciesConfig.currencies` соответствуют ключи локализации `{id}_name` /
  `{id}_description` в таблице `currencies` и спрайт `{id}_icon` в атласе `{id}_icon_atlas`.
- `Inventory` — Singleton: счётчики не пересоздаются при смене сцены.

## Как расширять

**Новая валюта.** Id в `CurrenciesConfig.json`, ключи локализации `{id}_name` / `{id}_description`,
спрайт `{id}_icon` в атласе `{id}_icon_atlas`, папка `Items/<Name>/Content/`. Кода менять не нужно —
фабрика подберёт её на следующем старте, а `AddNewItem` заведёт запись с нулём.

**Не-валютные предметы** (расходники, инвентарь с количеством): контракт `IInventory` уже про
«ключ + количество» и не завязан на валюты. Что придётся добавить — источник списка предметов
(сейчас это `CurrenciesConfig`) и различение типов в `ItemInfo`. Хранилище (`ItemsData`) менять не
нужно.

**Максимум/капы**: проверка в `ItemsData.AddItem` рядом с существующими, а не в `Inventory` — иначе
данные останутся способными принять значение выше капа в обход фасада.

**Транзакции** («отдай A, получи B» атомарно): метод на `IInventory`, принимающий две операции.
Реализовать через два вызова `Add`/`Remove` в вызывающем коде нельзя — между ними возможен
частичный результат.

**События экономики** (для аналитики или квестов): `Observable` на `IInventory` или сигнал в шину
(см. [[Signals]]). Подписка на `Info.Value` каждого счётчика для этого не годится — она не различает
причину изменения.

## Тесты

`Assets/Framework/Features/Tests/`:

| Файл | Что закрывает |
| --- | --- |
| `ItemsDataTests` | правила данных, отсутствие R3 в схеме, MemoryPack roundtrip в существующий инстанс |
| `ItemCounterTests` | синхронизация свойства с данными, отсутствие уведомления при отказе |
| `ItemCounterAllocationTests` | стоимость `BigInteger`: аллокации за пределами машинного слова |

Фейк для потребителей — `FakeInventory` (`Features/Tests/Fakes/`). `Inventory` как composition root
не тестируется: он только собирает фабрику и раздаёт вызовы, см. [[Testing-TDD]].

`ItemCounterAllocationTests` — редкий случай теста-замера: он существует, чтобы решение про
`ThrottleLastFrame` в `CurrencyView` не откатили как «лишнюю сложность».

## Когда обновлять

- Изменился контракт `IInventory` или `IItemCounter`.
- Изменились правила `ItemsData` (капы, отрицательные значения, новый член схемы).
- Появился источник предметов помимо `CurrenciesConfig`.
- Изменилась схема имён ассетов валюты или формат ключей локализации.
- `CurrencyView` перестал быть `MonoBehaviour` или сменил способ инжекта.
- Фича переехала между `Foundation` и `Features`.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Feature-Items") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Feature-Items") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
