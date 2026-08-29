---
title: LiveOps
type: architecture
area: Foundation
module: LiveOps
status: actual
source_paths:
  - Assets/Framework/Foundation/LiveOps/
  - Assets/Framework/Foundation/Initialization/Scripts/Registrators/LiveOps/
related:
  - "[[Configs]]"
  - "[[Time]]"
  - "[[Foundation-vs-Features]]"
  - "[[Signals]]"
tags:
  - architecture
  - liveops
  - foundation
updated: 2026-08-26
---

# LiveOps

## Для агента

Открывай эту статью, когда нужно подключить бэкенд (логин, серверное время, remote config) или
понять, откуда игра берёт серверные данные, когда сервера нет.

Главное, что стоит знать до начала работы: **в шаблоне бэкенда нет**, и это не «ещё не сделали», а
рабочее состояние. Все три контракта закрыты оффлайн-реализациями, игра стартует и играется без
сети. Подключение провайдера — это замена реализаций, а не включение подсистемы.

Второе: фича **никогда** не инжектит LiveOps-интерфейсы ради данных. Конфиги приходят как обычные
зависимости (см. [[Configs]]), время — через `IClock` (см. [[Time]]). Прямых потребителей
`IServerConnectionService` в `Features` сейчас нет: контракт и оффлайн-дефолт заведены под будущее
подключение провайдера.

## Назначение

LiveOps — это граница между игрой и бэкендом, выраженная минимальным набором интерфейсов. Смысл
границы в том, чтобы `Foundation` не знал ни одного SDK: PlayFab, GamePush и любой другой провайдер
живёт в собственном asmdef под `Integrations/` и подключается регистратором.

## Контракты

| Интерфейс | Что отвечает | Оффлайн-реализация |
| --- | --- | --- |
| `IServerConnectionService` | `bool IsConnectedToServer` | `OfflineServerConnectionService` — всегда `true` |
| `IRemoteConfigSource` | `IReadOnlyDictionary<string, string> GetValues()` | `EmptyRemoteConfigSource` — пустой набор |
| `IServerTimeSource` | `UniTask<Result<DateTime>> TryFetchUtc(ct)` | `LocalServerTimeSource` — `DateTime.UtcNow` |

Три решения в этих трёх строчках стоит проговорить:

- **`OfflineServerConnectionService` возвращает `true`, а не `false`.** Флаг означает «связь не
  сломана», а не «есть настоящий сервер». Фича, которая на `false` показывает ошибку, оффлайн вела
  бы себя как при аварии. Цена — флаг не годится для «покажи оффлайн-режим»; для этого нужен
  отдельный контракт.
- **`IServerTimeSource` возвращает `Result<DateTime>`, а не бросает.** Недоступный сервер — валидный
  сценарий, и `Clock` переводит часы в `ClockTrust.LocalFallback` вместо падения (см. [[Time]] и
  раздел про `Result<T>` в [[Utilities]]).
- **`IRemoteConfigSource` отдаёт `Dictionary<string, string>`, а не типизированные конфиги.**
  Разбором занимается `ConfigResolver`: значения из всех источников проходят один и тот же путь
  десериализации и одну политику отказа.

Фасада для «SDK со своим жизненным циклом» в контракте нет. Пустой `ILiveOpsController` здесь
лежал заготовкой и удалён как контракт без реализаций и потребителей:
интерфейс, который никто не исполняет, читается как обещание, которого нет. Провайдеру с
собственным старом достаточно `LifecycleEntity` в своей интеграции (образец — `YandexSdkEntity`,
см. [[Localization]]).

## Регистрация и подключение провайдера

`RootScope` вызывает `LiveOpsScopeRegistrator.Configure(builder)` (`RootScope.cs:17`), который
делает ровно два шага (`LiveOpsScopeRegistrator.cs:9-13`):

```csharp
RegisterOfflineDefaults(builder);   // три Singleton'а из LiveOps/Offline/
RegisterPlatform(builder);          // partial-метод, тело — под define
```

`RegisterPlatform` — `static partial void`: без реализации вызов вырезается компилятором, накладных
расходов нет. Тело живёт в отдельном файле-партиале под своим define-ом, по образцу
`LiveOpsScopeRegistrator.GamePush.cs`. Тот же паттерн у бэкендов сейва (`DataScopeRegistrator`) и у
рекламы (`AdsScopeRegistrator`) — см. [[Ads]].

Порядок подключения провайдера:

1. Asmdef в `Assets/Framework/Integrations/<Provider>/`, зависящий от `Foundation` и SDK провайдера.
   Обратной зависимости нет: `Foundation` про интеграцию не знает.
2. Реализации нужных интерфейсов (`IServerConnectionService`, `IRemoteConfigSource`,
   `IServerTimeSource`).
3. Партиал `LiveOpsScopeRegistrator.<Provider>.cs` с регистрацией под `#if <PROVIDER>_ENABLED`.
   Регистрация провайдера должна **перекрывать** оффлайн-дефолт — она идёт после него.
4. Триггер `ServerLoginCompletedSignal` после успешного логина: по нему `ConfigReader` забирает
   серверные значения.
5. Define в Project Settings → Player → Scripting Define Symbols. Это **ручной шаг пользователя**,
   агент его не выполняет.

## Единственное событие наружу

`ServerLoginCompletedSignal` (маркер, без payload) — всё, что LiveOps публикует. Подписчик один:
`ConfigReader` (`ConfigReader.cs:105`), который по нему вызывает `IRemoteConfigSource.GetValues()`
и отдаёт значения резолверу.

Сейчас у этого сигнала **нет источника** — единственный такой случай в шаблоне, см. каталог в
[[Signals]]. Практический вывод для того, кто подключает бэкенд: пока сигнал не триггерится,
серверные значения не попадут в конфиги, даже если `IRemoteConfigSource` реализован.

Момент логина относительно `IConfigProvider.WarmUp` не гарантирован. Если логин завершился позже
прогрева, уже созданные конфиги останутся из dummy — горячей перезагрузки конфигов в шаблоне нет
(см. [[Configs]], раздел «Как расширять»).

## Кто это потребляет

| Потребитель | Что берёт | Зачем |
| --- | --- | --- |
| `ConfigReader` | `IRemoteConfigSource` + сигнал логина | серверные значения конфигов |
| `Clock` | `IServerTimeSource` | разовая синхронизация часов в `WarmUp` |

`IServerConnectionService` зарегистрирован оффлайн-дефолтом, но прикладных потребителей в шаблоне
пока нет. `IServerTimeSource` инжектится **только** в `Clock`: прикладной код время берёт из
`IClock`, а не из источника синхронизации.

## Инварианты

- `Foundation` не ссылается ни на один SDK бэкенда: `grep -rn "PlayFab\|GamePush" Assets/Framework/Foundation`
  не даёт попаданий вне имён define-ов и партиалов регистратора.
- Каждый LiveOps-интерфейс имеет оффлайн-реализацию, зарегистрированную в `RegisterOfflineDefaults`.
  Проверка — `RegistrationGraphTests`: каждая `[Inject]`-зависимость зарегистрированного типа кем-то
  закрыта, и `LiveOpsScopeRegistrator.Configure` входит в тестовый граф (`RegistrationGraphTests.cs:153`).
- Игра стартует без сети: ни один путь инициализации не ждёт ответа сервера. `IServerTimeSource`
  возвращает `Result`, а не бросает; `IRemoteConfigSource` возвращает пустой набор, а не `null`.
- `RegisterPlatform` объявлен `static partial void` — вызов без реализации вырезается компилятором.
- Фичи не инжектят `IRemoteConfigSource` и `IServerTimeSource`.

## Как расширять

**Новый контракт бэкенда** (лидерборды, облачный сейв, покупки): интерфейс в `Foundation/LiveOps/`,
оффлайн-реализация в `LiveOps/Offline/`, регистрация в `RegisterOfflineDefaults`. Без оффлайн-дефолта
контракт не заводится — иначе шаблон перестанет собираться без провайдера.

**Отдельный флаг «настоящий сервер»** нужен, когда фича должна вести себя иначе оффлайн (например,
скрывать раздел магазина). Это второй член контракта или отдельный интерфейс, но **не** смена
семантики `IsConnectedToServer` без явного решения: оффлайн-дефолт всегда `true`, и фичи шаблона
на нём пока не завязаны.

**Собственный жизненный цикл SDK** оформляется `LifecycleEntity` в интеграции, а не вызовом из
`RootScope`: инициализация SDK асинхронна, а `Configure` — нет. Фаза `Load` такой сущности плюс
барьер между фазами дают потребителям готовый SDK без гонки.

**Ретрай логина.** Сейчас его нет: сигнал одноразовый, и повторной попытки подключения шаблон не
делает. Ретрай — часть реализации провайдера; шине про него знать не обязательно, достаточно
триггерить `ServerLoginCompletedSignal` при каждом успешном логине (`ConfigReader` обработает
повторный вызов, `SetServerValues` сам решит, писать ли кэш).

## Тесты

Собственных тестов у подсистемы нет — оффлайн-реализации не содержат логики. Проверяется она
косвенно:

- `RegistrationGraphTests` — что граф DI закрыт после `LiveOpsScopeRegistrator.Configure`.
- `ClockTests` с `FakeServerTimeSource` — поведение часов при успешном и неуспешном ответе.
- `ConfigReaderTests` с `FakeRemoteConfigSource` — реакция на `ServerLoginCompletedSignal`.

## Когда обновлять

- Добавлен или изменён контракт в `Foundation/LiveOps/`.
- Появилась реализация провайдера в `Integrations/` — таблица «Контракты» и раздел «Подключение»
  перестанут описывать реальность.
- `ServerLoginCompletedSignal` получил источник или второго подписчика.
- Изменилась семантика `IsConnectedToServer`.

## Last Verified

2026-08-26, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "LiveOps") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "LiveOps") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
