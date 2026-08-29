---
title: Logger
type: architecture
area: Foundation
module: Logger
status: actual
source_paths:
  - Assets/Framework/Foundation/Logger/
  - Assets/Framework/Foundation/Utilities/Scripts/EntityStatus.cs
  - Tools/AutoDecorators.Generator/AutoDecoratorsGenerator.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[UI-Views]]"
tags:
  - architecture
  - logger
  - foundation
updated: 2026-08-26
---

# Logger

## Для агента

Открывай эту статью, если нужно что-то залогировать, добавить логи в новый класс, понять, почему
логов не видно, или зачем в коде `if (!Logger.AreLogsEnabled) return;`.

Три правила, которые закрывают 95% случаев:

1. `Debug.Log*` напрямую не вызывается — только `ILogChannel`.
2. Канал не создаётся руками — вешается `[AutoLogger("Name")]` на `partial`-класс, дальше `Logger`.
3. Хот-пасс логируется под guard `AreLogsEnabled`, и **форматирование строки лежит внутри guard-а**.

## Назначение

Подсистема решает три задачи, которых нет у голого `Debug.Log`:

- **Атрибуция.** У каждой записи есть имя источника и цвет по категории — в консоли видно, кто пишет.
- **Verbosity по источникам.** Канал выключается точечно (`SetLogsStatus(false)`), а не весь лог сразу.
- **Тестируемость.** `ILogChannel` инжектится, значит в EditMode-тесте подставляется `FakeLogChannel`
  и логи не заваливают Test Runner (см. [[Testing-TDD]]).

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `ILogChannel` | контракт канала: `Log`, `LogError`, `AreLogsEnabled`, `SetLogsStatus`, `EntityType` |
| `LogChannel` / `LogChannel<T>` | реализация; generic-вариант берёт имя из `typeof(T).Name` |
| `ILogChannelFactory` / `LogChannelFactory` | Singleton-фабрика с кэшем каналов |
| `LogCategory` | `System` \| `Feature` — определяет цвет префикса |
| `LoggerConstants` | цвета и формат строки |
| `LoggerStringExtensions` | `FormatAs*Log` (весь префикс) и `Set*Color` (подсветка значения) |

Категорий ровно две, и они не про важность, а про слой: `System` (зелёный `#4CA57D`) — инфраструктура
`Foundation`, `Feature` (синий `#3D77FF`) — игровая механика. Красный `#CE342A` не выбирается —
им всегда красится `LogError` (`LoggerConstants.cs:7-10`).

## Как получить канал

Порядок предпочтения — сверху вниз, вниз спускаемся только когда верхнее физически не работает.

**1. `[AutoLogger]` — дефолт.**

```csharp
[AutoLogger(SettingsConstants.LogName, LogCategory.Feature, StatusLogs = true)]
public partial class SettingsCore : LifecycleEntity
{
    private void Foo() => Logger.Log("hello");
}
```

Генератор эмитит в partial-часть свойство `Logger` и `[Inject]`-метод `__InitAutoLogger`, который
берёт канал из `ILogChannelFactory` (`AutoDecoratorsGenerator.cs:325-347`). Работает в любом типе,
который строит VContainer, — не только в `LifecycleEntity` и не только в `Features`. Класс обязан
быть `partial`, иначе `ADG001`. Подробности атрибутов — [[Initialization-LifecycleEntity]].

Сеттер `Logger` приватный, но внутри самого типа доступен — поэтому тестовый шов
(`internal`-конструктор) просто присваивает `Logger = logger`, и фабрика в тесте не нужна
(`SaveLoadService`, `SceneLoader`).

**2. `ILogChannelFactory` напрямую** — когда логгер нужен раньше post-inject: в конструкторе или
внутри собственного `[Inject]`-метода. `[AutoLogger]` там не подходит, потому что **двух
`[Inject]`-методов на одном типе быть не должно**: порядок их вызова VContainer не определяет.
Примеры — `ConfigReader`, `AnalyticsController`, `SaveEnvelope`, `ViewRouter`.

**3. Параметр конструктора** — объект создаётся вручную вне контейнера
(`SceneStateMachine` → `*SceneState`).

**4. `new LogChannel<T>()`** — только там, где контейнера нет физически. Список закрытый, каждый
случай помечен комментарием «почему»: `SaveLoadMenu.cs:14` (editor),
`BootstrapPlayButton.cs:24` (editor), `VContainerBuilderExtensions.cs:41`, `EntityStatus.cs:27`.

## Verbosity и guard хот-пасса

Канал включён по умолчанию (`LogChannel.cs:9`, тест `LogChannelFactoryTests.AreLogsEnabled_IsTrue_ByDefault`).
`SetLogsStatus(false)` гасит **только** `Log`; `LogError` пишется всегда — иначе выключение verbosity
молча проглотило бы реальные ошибки (`LogChannel.cs:41-47`).

Отсюда правило хот-пасса. `Log(...)` сам проверяет флаг внутри, но аргумент вычисляется **до**
вызова: интерполяция строки и `.SetFeatureColor()` отработают даже при выключенных логах и оставят
мусор в GC. Поэтому в путях, которые исполняются чаще раза в кадр или на каждое действие игрока,
guard стоит на стороне вызывающего, а форматирование — внутри него:

```csharp
private void LogOperation(...)
{
    if (!Logger.AreLogsEnabled)
    {
        return;
    }

    Logger.Log($"{id.SetFeatureColor()} {amount.ToString().SetFeatureColor()}");
}
```

Примеры — `ClickerModel.cs:57`, `Inventory.cs:95`, `AdsController.cs:183`. `LogError` в guard не
заворачивается.

## Логи статусов сущностей

`EntityStatus` держит **собственный** `LogChannel`, созданный в конструкторе: статус создаётся до
инжекта, фабрику там взять негде (`EntityStatus.cs:23-32`). Этот канал по умолчанию **выключен**
(`areLogsEnabled: false`) — иначе консоль на старте забивал бы каждый переход каждой сущности.

Включается он одним из двух способов, и оба сводятся к `EntityStatus.EnableLogging`:

- `[AutoLogger(..., StatusLogs = true)]` — генератор дописывает вызов `EnableStatusLogs(entityType)`
  в `__InitAutoLogger` (`AutoDecoratorsGenerator.cs:343`). На классе не-`LifecycleEntity` это `ADG002`.
- `EnableStatusLogs(...)` вручную из `Init` — остаётся для классов вне иерархии `LifecycleEntity`.

Логируются только фактические переходы: подписка идёт через `DistinctUntilChanged().Skip(1)`
(`EntityStatus.cs:88-95`), так что повторный `SetEnabled(true)` в консоль не попадает.

Канал статусов и канал `Logger` — **разные объекты**: у первого имя приходит из
`EntityStatus(entityName)`, у второго — из ключа `[AutoLogger]`. Совпадение имён желательно, но
компилятор его не требует.

## Инварианты

- В коде нет вызовов `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` вне `LogChannel.cs`.
  Проверка: `grep -rn "Debug\.Log" Assets/Framework --include=*.cs` вне `Tests/` даёт ровно два
  вызова (`LogChannel.cs:38,46`) и одно упоминание в doc-комментарии `ReactiveSignalBus.cs:15`.
- `new LogChannel` встречается только в шести файлах, перечисленных выше, и в `LogChannelFactory`.
  Новое попадание без комментария «почему» — ошибка ревью.
- `LogError` никогда не стоит под guard `AreLogsEnabled`.
- Форматирование аргумента лога (интерполяция, `Set*Color`) в хот-пассе не выполняется вне guard-а.
- Класс с `[AutoLogger]` объявлен `partial` (иначе `ADG001`) и не имеет второго `[Inject]`-метода.
- `[AutoLogger(StatusLogs = true)]` стоит только на наследниках `LifecycleEntity` (иначе `ADG002`).
- Поле логгера руками не объявляется: в классе с `[AutoLogger]` нет `private readonly ILogChannel`.
- `LogChannelFactory` кэширует по паре `(имя, категория)` — один и тот же ключ отдаёт один инстанс
  (`LogChannelFactoryTests.Get_ReturnsSameInstance_ForSameNameAndType`).

## Как расширять

**Новая категория** (например `LogCategory.Network`): значение в enum, цвет в `LoggerConstants.Colors`,
ветка в `LogChannel.Format` (`LogChannel.cs:49-57`) и в `EntityStatus.LogStatusChange`. Контракт
`ILogChannel` при этом не меняется.

**Уровень Warning.** Сейчас уровня между `Log` и `LogError` нет сознательно: в шаблоне запись либо
информационная и гасится verbosity, либо ошибка и пишется всегда. Добавление третьего уровня
обязано ответить, гасится ли он `SetLogsStatus`, — иначе появится третья, необъявленная политика.

**Вывод не в консоль** (файл, remote-логгер): вторая реализация `ILogChannel` и составной канал в
`LogChannelFactory.Get`. Точка одна — фабрика; потребители не меняются.

**Глобальный тумблер логов** (из настроек или дев-инструмента): фабрика знает все выданные каналы,
поэтому «выключить всё» добавляется методом на `LogChannelFactory`, а не обходом потребителей.

## Тесты

`Assets/Framework/Foundation/Tests/LogChannelFactoryTests.cs` — кэш фабрики и поведение флага.

В тестах чужих подсистем логгер подставляется фейком: `FakeLogChannel` (когда `ILogChannel`
инжектится напрямую) и `FakeLogChannelFactory` (когда классу нужна фабрика). `LogAssert` используется
только там, где проверяется именно факт записи в Unity-консоль, — вне Unity такие тесты помечаются
SKIPPED, см. [[Testing-TDD]].

## Когда обновлять

- Добавлено значение в `LogCategory` или цвет в `LoggerConstants`.
- Изменился контракт `ILogChannel` (новый метод, новый уровень).
- Появился ещё один легальный случай `new LogChannel` — список в разделе «Как получить канал»
  обязан оставаться исчерпывающим.
- Генератор поменял форму эмита `Logger` / `__InitAutoLogger` или добавил диагностику про `[AutoLogger]`.
- Изменилось поведение логов статусов в `EntityStatus`.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Logger") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Logger") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
