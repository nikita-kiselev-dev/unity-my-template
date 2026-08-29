---
title: Analytics
type: architecture
area: Foundation
module: Analytics
status: actual
source_paths:
  - Assets/Framework/Foundation/Analytics/
  - Assets/Framework/Features/Clicker/Scripts/ClickerAnalytics.cs
  - Assets/Framework/Features/DailyBonus/Scripts/DailyBonusAnalytics.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Logger]]"
  - "[[Naming]]"
tags:
  - architecture
  - analytics
  - foundation
updated: 2026-08-08
---

# Analytics

## Для агента

Открывай эту статью, когда фиче нужно отправить событие или подключить аналитическую систему.

Ключевое решение, которое надо принять до кода: **фича не отправляет события напрямую**. Она
инжектит свой `I<Feature>Analytics` с методами на языке домена (`LogUpgrade(int level)`), а
конструирование `AnalyticsEvent` живёт в реализации рядом с фичей. Так имена событий и параметров
не расползаются по вызывающему коду, и их можно поменять в одном файле.

Отправка нигде не бросает и ничего не возвращает: отсутствие живого сервиса — это `LogError`, а не
исключение. Аналитика не должна ломать геймплей.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `IAnalyticsController` / `AnalyticsController` | фасад: `SendEvent`, роутинг по сервисам, Singleton |
| `IAnalyticsEvent` / `AnalyticsEvent` | событие: имя, параметры, набор адресатов |
| `IAnalyticsService` | адаптер к конкретной системе (Amplitude, Firebase, …) |
| `AnalyticsConstants` | формат параметра в логе, ключи Amplitude |
| `I<Feature>Analytics` / `<Feature>Analytics` | доменный фасад фичи, живёт в `Features/` |

`IAnalyticsService` — единственное место в шаблоне, где `Service` означает ровно то, что по
конвенции: тонкий адаптер к внешней системе (см. [[Naming]]).

## Событие

`AnalyticsEvent` строится чейном:

```csharp
var analyticsEvent = new AnalyticsEvent(ClickerConstants.Analytics.UpgradeName)
    .AddParameter(ClickerConstants.Analytics.UpgradeParameterLevel, level);

_analyticsController.SendEvent(analyticsEvent);
```

Три детали:

- **Словарь параметров ленивый** (`AnalyticsEvent.cs:26-31`): у события без параметров его нет
  вовсе. Событий много, и половина из них — просто имя. Наружу `Parameters` уходит как
  `IReadOnlyDictionary`, `Services` — как `IReadOnlyCollection`: собственные `Dictionary`/`HashSet`
  private, менять их можно только через `AddParameter` / `To<T>` (см. [[Class-Interaction]]).
  У события без параметров `Parameters` равен `null` — это и есть признак «параметров нет»,
  `ToString()` проверяет именно его.
- **`AddParameter` использует `Add`, а не индексатор** — дубль ключа бросит. Это осознанно: два
  значения под одним ключом означают ошибку в коде вызывающего, а не «последнее выигрывает».
- **`ToString()` входит в контракт `IAnalyticsEvent`**, потому что событие само себя форматирует
  для лога — с подсветкой через `SetSystemColor()`, см. [[Logger]].

## Роутинг: несколько систем сразу

`AnalyticsController` держит словарь `Type → IAnalyticsService` и заполняет его в `[Inject]`-методе:
инжектится `IReadOnlyList<IAnalyticsService>` (VContainer отдаёт все зарегистрированные реализации),
у каждой зовётся `Init()`, и в словарь попадают **только те, у кого `IsInited == true`**
(`AnalyticsController.cs:44-55`). Сервис, у которого не поднялся SDK, дальше просто не существует.

Адресаты события задаются на самом событии:

| Как построено | Куда уйдёт |
| --- | --- |
| без `.To<T>()` | во **все** живые сервисы |
| `.To<AmplitudeService>()` | только в указанные; каждый недоступный — отдельный `LogError` |

Смысл в том, что решение «куда» принимает автор события, а не конфигурация контроллера: событие
покупки нужно всем системам, а технический дебаг-ивент — одной.

Логгер здесь берётся через `ILogChannelFactory`, а не `[AutoLogger]`, потому что у класса уже есть
свой `[Inject]`-метод, а порядок вызова двух таких методов VContainer не определяет
(`AnalyticsController.cs:35-42`) — общее правило описано в [[Logger]].

## Где объявлять события фичи

Имена и ключи параметров — в `<Feature>Constants.Analytics`, рядом с остальными константами фичи.
В `Foundation` их быть не должно: имена событий — это про игру, а не про инфраструктуру
(см. [[Foundation-vs-Features]]).

Полный путь для новой фичи:

1. `<Feature>Constants.Analytics` — константы имени события и ключей параметров.
2. `I<Feature>Analytics` — методы на языке домена, без единого упоминания `AnalyticsEvent`.
3. `<Feature>Analytics` — реализация, принимает `IAnalyticsController` конструктором.
4. `<Feature>Core` создаёт её через `new` и передаёт в Model/ViewModel — как и остальные
   зависимости фичи, см. [[UI-MVVM]].

В тесте вместо неё подставляется фейк (`FakeClickerAnalytics`), и модель тестируется без
контроллера вообще.

## Состояние подключения

**Ни одной реализации `IAnalyticsService` в шаблоне нет.** `AnalyticsConstants.Amplitude` содержит
имя и пустой `ApiKey` — это заготовка, а не рабочая интеграция. Практическое следствие: `SendEvent`
сегодня всегда попадает в ветку «нет активных сервисов» и пишет `LogError`.

Это отличается от политики LiveOps, где у каждого контракта есть оффлайн-дефолт (см. [[LiveOps]]).
Здесь дефолта нет сознательно: «отправить в никуда» — не то же самое, что «работать оффлайн», и
громкая ошибка в логе честнее молчаливой заглушки, которая выглядит как работающая аналитика.

## Инварианты

- Фича не создаёт `AnalyticsEvent` вне своего `<Feature>Analytics`. Проверка:
  `grep -rn "new AnalyticsEvent" Assets/Framework --include=*.cs` вне `Tests/` даёт попадания
  только в `*Analytics.cs` файлах фич.
- Имена событий и ключи параметров лежат в `<Feature>Constants.Analytics`, а не строковыми
  литералами в месте вызова.
- `SendEvent` не бросает и не возвращает результат: любая проблема — `LogError`.
- В словарь контроллера попадают только сервисы с `IsInited == true`.
- `AnalyticsController` инжектит `IReadOnlyList<IAnalyticsService>`, а не конкретные сервисы —
  добавление системы не меняет контроллер.
- У `AnalyticsController` пара ctor-ов «`[Inject]` + шов», атрибут на публичном обязателен
  (правило `injectable-ctor-missing-attribute` в `Tools/naming-check.ps1`).

## Как расширять

**Новая аналитическая система.** Класс, реализующий `IAnalyticsService`, в
`Assets/Framework/Integrations/<Provider>/` (свой asmdef, зависит от SDK), регистрация через
partial-регистратор под define — тем же паттерном, что LiveOps и реклама. Контроллер не меняется:
он подберёт сервис из инжектируемого списка.

`Init()` обязан выставить `IsInited` честно: сервис, соврaвший про успешную инициализацию, будет
получать события в никуда, и контроллер об этом не узнает.

**Событие сразу в несколько конкретных систем** — цепочка `.To<A>().To<B>()`; `Services` это
`HashSet`, повтор безопасен.

**Батчинг и оффлайн-очередь.** Сейчас `SendEvent` синхронный и без буфера: событие, отправленное до
инициализации сервисов, теряется. Очередь добавляется в контроллер (буфер до первого успешного
`Init`), а не в каждый сервис.

**Обязательные параметры** (версия сборки, id игрока) — в контроллер перед раздачей по сервисам,
чтобы не дублировать их в каждом `<Feature>Analytics`. Точка врезки — `SendEvent`, до ветвления
на `SendToAll`/`SendToCertain`.

## Тесты

`Assets/Framework/Foundation/Tests/AnalyticsControllerTests.cs` — пять сценариев роутинга:
отправка во все сервисы без адресатов, только в указанный при `.To<T>()`, пропуск сервиса с
неудавшимся `Init`, `LogError` при отсутствии живых сервисов и при недоступном адресате.

Фейки: `FakeAnalyticsService` (управляемый `IsInited`, собирает полученные события) и
`FakeClickerAnalytics` для тестов моделей фич. Логгер подставляется через шов-конструктор
(`FakeLogChannel`), см. [[Testing-TDD]].

## Когда обновлять

- Появилась первая реализация `IAnalyticsService` — раздел «Состояние подключения» станет неверным.
- Изменился контракт `IAnalyticsEvent` или `IAnalyticsService`.
- Изменилась логика роутинга (`SendToAll` / `SendToCertain`) или отбор по `IsInited`.
- Появились обязательные параметры, батчинг или очередь событий.
- Изменилось правило, где объявляются имена событий фичи.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Analytics") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Analytics") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
