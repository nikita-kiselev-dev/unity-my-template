---
title: Time
type: architecture
area: Foundation
module: Time
status: actual
source_paths:
  - Assets/Framework/Foundation/Time/IClock.cs
  - Assets/Framework/Foundation/Time/Clock.cs
  - Assets/Framework/Foundation/Time/ClockTrust.cs
  - Assets/Framework/Foundation/Time/IRealtimeSource.cs
  - Assets/Framework/Foundation/Time/StopwatchRealtimeSource.cs
  - Assets/Framework/Foundation/LiveOps/IServerTimeSource.cs
  - Assets/Framework/Foundation/LiveOps/Offline/LocalServerTimeSource.cs
  - Assets/Framework/Foundation/Initialization/Scripts/SceneStarter.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Testing-TDD]]"
tags:
  - architecture
  - foundation
  - time
  - live-ops
  - r3
updated: 2026-08-26
---

# Time

## Для агента

Открывай эту статью, если механике нужно текущее время, обратный отсчёт, дедлайн ивента или суточный сброс.

Главное правило: **время читается синхронно**. `IClock.ServerUtcNow` — обычное свойство, никакого `await`. Если ты пишешь `async` метод только чтобы получить время — ты делаешь не то.

Второе правило: выбирай источник времени осознанно. Их три, и они отвечают на разные вопросы (см. «Какое время брать»).

## Назначение

Серверное время синхронизируется один раз до готовности игры, дальше часы идут локально по монотонному источнику. Асинхронность остаётся только в точке синхронизации — `IServerTimeSource`.

Это убирает две проблемы прямого «спроси сервер, когда надо»: ожидание сети в геймплее и протекание `async` по всей цепочке вызовов.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `IClock` / `Clock` | единая точка доступа к времени, Singleton |
| `ClockTrust` | `LocalFallback` \| `ServerVerified` — можно ли доверять `ServerUtcNow` |
| `IRealtimeSource` / `StopwatchRealtimeSource` | монотонный ход часов между синхронизациями |
| `IServerTimeSource` / `LocalServerTimeSource` | источник синхронизации, единственное async-место |

## Какое время брать

| Нужно | Член | Почему |
| --- | --- | --- |
| Награды, ивенты, кулдауны | `ServerUtcNow` | синхронизировано и монотонно; неподделываемо только при `Trust == ServerVerified` |
| Сброс в местную полночь | `ServerLocalNow` | честное серверное время в таймзоне игрока; так работает `DailyBonus` |
| Тикающее время для UI | `ServerNow` | `ReadOnlyReactiveProperty<DateTime>`, тик раз в секунду |
| Обратный отсчёт до дедлайна | `Countdown(deadlineUtc)` | убывающий `TimeSpan`, завершается ровно на `TimeSpan.Zero` |

Оговорка про «игрок не подделает»: она держится, пока `Trust == ClockTrust.ServerVerified`. При `LocalFallback` (сервер не ответил или `WarmUp` ещё не прошёл) anchor взят с локальных часов — время монотонно внутри сессии, но его абсолютное значение задал игрок. Механика, для которой это критично (выдача награды, дедлайн ивента), обязана смотреть на `Trust`, а не считать `ServerUtcNow` доверенным всегда.

Часов устройства в контракте нет. `DeviceNow` (`DateTime.Now`) был объявлен под «оффлайн-таймеры по часам игрока» и удалён: ни одна механика шаблона его не позвала, а недоверенное время рядом с доверенным — приглашение взять не то. Понадобятся часы устройства — вернуть вместе с механикой, которая их читает.

`ServerLocalNow` — готовое свойство, а не композиция «сдвинь UTC в местную зону»: сдвиг лежит внутри `Clock` (`private static ToDeviceTimeZone`) и наружу не торчит. Иначе рядом с правильным `ToDeviceTimeZone(ServerUtcNow)` компилировался бы `ToDeviceTimeZone(DateTime.Now)` — двойной сдвиг. Понадобится перевести в местную зону произвольный UTC (например, сохранённую дату) — метод вернётся в контракт тогда.

## Инварианты

- В игровой логике нет `DateTime.UtcNow` / `DateTime.Now` — только `IClock`. Исключения: сам `Clock` и `LocalServerTimeSource`.
- Ход часов между синхронизациями — `IRealtimeSource`, а не системные часы. Иначе игрок переводит время посреди сессии и ломает или читит все таймеры.
- `IRealtimeSource` — **не** инжектируемый `TimeProvider` из `RootScope`: в плеере это `UnityTimeProvider.Update` с `TimeKind.Time`, его `GetTimestamp()` отдаёт `Time.timeAsDouble` — зависит от `timeScale` и стоит на паузе игры. `TimeProvider` при этом остаётся источником *интервалов* для `ServerNow` и `Countdown`.
- `WarmUp` идемпотентен: `SceneStarter` зовёт его на каждой сцене, синхронизация происходит один раз за процесс. Флаг «прогрет» ставится только после успешного прохода — отменённый `WarmUp` (teardown scope посреди синхронизации) обязан ретраиться на следующей сцене, иначе часы навсегда остаются на `LocalFallback`. То же правило у `ConfigProvider.WarmUp`: неуспешный результат не мемоизируется.
- Часы работают с первой секунды процесса: до `WarmUp` они идут от локального времени, а `Trust` равен `LocalFallback`. Геттер времени никогда не бросает — механике достаточно знать про недоверие.
- Сервер не ответил → anchor от локального времени, `Trust = LocalFallback`, запись в лог. Игра стартует.
- Ресинхронизация — по `ApplicationPauseChangedSignal(false)`: уход в background может заморозить процесс, и монотонный тик отстанет от реального времени.
- `Countdown` завершается, выдав `TimeSpan.Zero`, и никогда не отдаёт отрицательные значения.

## Как синхронизируется

`SceneStarter.StartAsync` прогревает время параллельно с конфигами, до резолва `LifecycleEntity`:

```csharp
await UniTask.WhenAll(
    _configProvider.WarmUp(cancellation),
    _clock.WarmUp(cancellation));
```

Так время готово раньше любой фазы любой сцены — порядок инициализации потребителей на корректность не влияет.

Внутри: `ServerUtcNow => _anchorUtc + (_realtime.Elapsed - _anchorElapsed)`.

## Как расширять

**Реальный серверный источник** (PlayFab, GamePush и т.п.): реализовать `IServerTimeSource` в `Assets/Framework/Integrations/<Provider>/`, зарегистрировать через partial-паттерн `LiveOpsScopeRegistrator.RegisterPlatform` под соответствующим define. `Clock` менять не нужно.

**Античит** (детект отката часов): добавить `ClockTrust.Untrusted`, персистить last-seen-время в `Data` и сверять при синхронизации. Контракт `IClock` при этом не меняется — значение enum-а добавляется.

**Реакция механики на прыжок времени** при ресинке: добавить payload-сигнал (`TimeResyncedSignal(TimeSpan delta)`) и триггерить его из `Synchronize`. Пока такой механики нет: `DailyBonus` оценивает состояние один раз в `Init`.

## Тесты

`Assets/Framework/Foundation/Tests/ClockTests.cs`. Фейки: `FakeServerTimeSource`, `FakeRealtimeSource`, `FakeTimeProvider`, `FakeLogChannelFactory`.

В тесте две независимые оси времени, как и в проде: `FakeRealtimeSource.Advance` двигает часы, `FakeTimeProvider.Advance` — интервалы тиков. Для проверки `ServerNow` / `Countdown` двигать нужно обе.

## Namespace

`Framework.Foundation.Time` затеняет `UnityEngine.Time` внутри своих файлов — если там понадобится Unity-овский `Time`, обращаться полным именем. Аналогичная ситуация с сегментом `ViewModel` описана в [[UI-MVVM]].

## Когда обновлять

- Появился новый член `IClock` или изменилась семантика существующего.
- Сменился монотонный источник или точка синхронизации.
- Добавлено значение в `ClockTrust`.
- Появилась реализация `IServerTimeSource` поверх реального бэкенда.

## Last Verified

2026-07-26, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Time") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Time") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```