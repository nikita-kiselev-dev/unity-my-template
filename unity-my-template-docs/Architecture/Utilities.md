---
title: Utilities
type: architecture
area: Foundation
module: Utilities
status: actual
source_paths:
  - Assets/Framework/Foundation/Utilities/
related:
  - "[[Naming]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Logger]]"
tags:
  - architecture
  - utilities
  - foundation
updated: 2026-08-26
---

# Utilities

## Для агента

Открывай эту статью в двух случаях: нужен `Result<T>` / `EntityStatus` / готовое расширение, или
хочется положить в `Utilities/` что-то новое.

Правило «что сюда попадает» одно и жёсткое: **тип с ролью, но без дома**. Не «мелочь», не «пока
некуда» — именно то, у чего нет своей подсистемы, но есть внятная роль, выраженная именем.
`Result<T>` (исход операции), `EntityStatus` (статус сущности), `FrameRateLimiter` (лимит FPS) —
законны. `ItemsUtils` или `SaveHelper` — нет, и не из-за суффикса: у них есть дом (`Items`, `SaveLoad`).

Проверка себя одним вопросом: **«кто-то будет искать этот тип здесь?»** Если ответ «нет, он бы
искал его в своей фиче» — тип кладётся в фичу.

## Состав

| Тип | Роль |
| --- | --- |
| `Result<T>` | исход операции, у которой «нет значения» — валидный сценарий |
| `EntityStatus` | `IsEnabled` / `IsInited` / `IsActive` + логи переходов |
| `IEntityStatus` / `IReadOnlyEntityStatus` | контракты статуса: полный и только для чтения |
| `FrameRateLimiter` / `TargetFrameRate` | `MonoBehaviour`, ставит `Application.targetFrameRate` |
| `ExternalLinkOpener` / `IExternalLinkOpener` / `ExternalLinks` | открытие внешних ссылок |
| `ChildComponentInjector` | инжект в дочерние компоненты по атрибуту на родителе |
| `Extensions/` | расширения, один файл на расширяемый тип |

## Result&lt;T&gt;

`readonly struct` с двумя полями и без аллокаций (`Result.cs:5-36`):

```csharp
public Result<ItemInfo> TryGetItem(string id)
{
    return _data.Items.TryGetValue(id, out var item)
        ? Result<ItemInfo>.Success(item)
        : Result<ItemInfo>.Failure();
}

if (_shop.TryGetItem("gem_pack_1").TryGet(out var item))
{
    Use(item);
}
```

Помимо `Value`/`HasValue` есть `TryGet(out)` и `GetValueOrDefault(fallback)`. `Map` и `Match`
были и удалены: их не вызывал ни один потребитель, только собственные
тесты. Понадобится цепочка преобразований — вернуть по живому вызову, а не заранее.

Зачем он вообще нужен: он отделяет **«значения нет»** от **«что-то сломалось»**. Отсутствие
значения — часть контракта и видно в сигнатуре; поломка остаётся исключением. Отсюда правило
проекта: защитные `null`-проверки «на всякий случай» не пишутся. То, что пришло через DI или лежит
в обязательной сериализованной ссылке префаба, обязано существовать, и `if (x == null) return;`
маскирует ошибку вместо того, чтобы её выявить.

Где он в шаблоне используется: `IConfigReader.Read` (`Result<IConfig>`), `IServerTimeSource.TryFetchUtc`
(`Result<DateTime>`), `ItemsData.GetValue` (`Result<BigInteger>`). Во всех трёх случаях промах —
нормальная ветка, а не авария.

Особый случай — `ConfigReader`: исключение десериализации ловится внутри и превращается в
`Result.HasValue = false`, чтобы решение «падать или нет» принимал уровень выше (см. [[Configs]]).

## EntityStatus

Три булевых статуса, каждый — `ReactiveProperty<bool>` внутри, наружу только геттеры
(`EntityStatus.cs:9-21`). Семантика статусов и то, кто их выставляет, описаны в
[[Initialization-LifecycleEntity]]; здесь — устройство самого типа.

- **Собственный логгер.** Статус создаётся в конструкторе `LifecycleEntity`, до инжекта, поэтому
  канал он делает сам через `new LogChannel` — один из пяти легальных случаев, см. [[Logger]].
  По умолчанию логи выключены (`areLogsEnabled: false`), включает их `EnableLogging`, за которым
  стоит `[AutoLogger(StatusLogs = true)]`.
- **Логируются только переходы:** подписка идёт через `DistinctUntilChanged().Skip(1)`
  (`EntityStatus.cs:88-95`), повторный `SetEnabled(true)` в консоль не попадает.
- **Сеттеры чейнятся** (`SetEnabled(true).SetInited(true)`) и **no-op после `Dispose`**
  (`EntityStatus.cs:37-65`): VContainer может повторить `Dispose` сущности при teardown scope, и
  сбросы статусов не должны бить по уже задиспозенным `ReactiveProperty`.

Два интерфейса вместо одного — это разделение прав: `IEntityStatus` отдаёт `Status` целиком (может
менять), `IReadOnlyEntityStatus` — только три флага. Потребитель, которому нужно наблюдать чужую
сущность, берёт read-only-версию.

## Остальное

**`FrameRateLimiter`** — `MonoBehaviour` с сериализованным `TargetFrameRate` (30/60/120). В `Awake`
снимает vSync и ставит `Application.targetFrameRate`. Значение выбирается в Inspector, кода менять
не нужно.

**`ExternalLinkOpener`** — единственный легальный `Application.OpenURL` в проекте; URL-ы лежат в
`ExternalLinks`. Потребитель — `MainMenuViewModel` (кнопка privacy policy). Смысл обёртки в
тестируемости: во ViewModel инжектится `IExternalLinkOpener`, в тесте — `FakeExternalLinkOpener`.

**`ChildComponentInjector`** — статический хелпер для одного узкого случая: у view есть
атрибут-маркер, и всем дочерним компонентам типа `U` нужно раздать зависимости. Используется из
`IViewSetupStep` (`CurrencyViewSetupStep.cs:15`), см. [[UI-Views]]. Прямо из фичи не вызывается.

**`Extensions/`** — по файлу на расширяемый тип: `Button`, `Slider`, `GameObject`, `Canvas`,
`string`, `IEnumerable`, `UniTask`, `CancellationToken`. Два расширения стоит знать:

- `AddListenerClean` (`Button`, `Slider`) — сначала `RemoveListener`, потом `AddListener`. Повторный
  проход не задваивает обработчик; так `ButtonSoundBinder` переживает повторную настройку одного view.
- `UniTask.Forget(ILogChannel)` — «выстрелил и забыл», но с логом исключения. Голый `Forget()`
  проглатывает ошибку молча; когда результат не нужен, а знать о падении хочется, берётся эта
  перегрузка (есть и вариант с `Action<Exception>`).

## Инварианты

- Ни один тип в `Utilities/` не называется `*Utils`, `*Helper`, `*Manager`, `*Handler` — правило
  `forbidden-suffix` в `Tools/naming-check.ps1`.
- В `Utilities/` нет типов, привязанных к конкретной механике игры: там нет `using` на
  `Framework.Features.*`. `Foundation` вообще не ссылается на `Features` — см. [[Foundation-vs-Features]].
- `Result<T>` — `readonly struct`: без аллокации на каждый промах.
- Отсутствие значения выражается `Result<T>`, а не `null` + защитная проверка у вызывающего.
- `Application.OpenURL` вызывается только в `ExternalLinkOpener`. Проверка:
  `grep -rn "Application.OpenURL" Assets/Framework --include=*.cs`.
- Сеттеры `EntityStatus` после `Dispose` не бросают и ничего не меняют.
- Файл расширений называется по расширяемому типу (`ButtonExtensions`, `StringExtensions`), и один
  тип не расширяется из двух файлов.

## Как расширять

**Новое расширение.** Есть файл под этот тип — метод туда; нет — новый `<Тип>Extensions.cs`.
Расширение не должно тянуть зависимости: если методу нужен сервис, это не расширение, а сервис.

**Новый тип в `Utilities/`.** Сначала ответить, почему у него нет своего дома. Если ответ «он нужен
двум фичам» — возможно, дом есть, просто он ещё не заведён: две фичи, которым нужна одна механика,
обычно означают новую подсистему `Foundation`, а не запись в общей папке.

**`Result<T>` с причиной ошибки.** Сейчас неуспех безымянный. Причина добавляется отдельным типом
(`Result<T, TError>`), а не полем `string Error` в существующем: иначе каждый промах начнёт
аллоцировать строку, а `readonly struct` без аллокаций — половина смысла типа.

**Статус сущности** расширяется только вместе с [[Initialization-LifecycleEntity]]: четвёртый флаг
означает четвёртую фазу или четвёртое состояние, и это решение про lifecycle, а не про утилиты.

## Тесты

`Assets/Framework/Foundation/Tests/`:

- `ResultTests` — `Success`/`Failure`, `TryGet`, `GetValueOrDefault`.
- `EntityStatusTests` — только поведение после `Dispose`: сеттеры не бросают, повторный `Dispose`
  не бросает. Сами переходы и логи проверяются косвенно, через `LifecycleGateTests` и тесты
  конкретных сущностей.

Расширения отдельными тестами не покрываются: каждое — две-три строки без ветвлений, и они
проверяются через потребителей. Расширение, которому понадобился свой тест, — сигнал, что в нём
завелась логика и ему пора стать типом с именем.

## Когда обновлять

- В `Utilities/` добавлен или удалён тип — таблица «Состав» обязана оставаться полной.
- Изменился контракт `Result<T>` или `EntityStatus`.
- Появился второй легальный вызов `Application.OpenURL` или второй способ выставлять FPS.
- Изменилось правило «что попадает в `Utilities`».
- Добавлен файл в `Extensions/` для типа, которого раньше не было.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Utilities") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Utilities") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
