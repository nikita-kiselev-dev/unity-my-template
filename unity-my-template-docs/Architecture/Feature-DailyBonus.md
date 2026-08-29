---
title: Feature-DailyBonus
type: architecture
area: Features
module: DailyBonus
status: actual
source_paths:
  - Assets/Framework/Features/DailyBonus/
related:
  - "[[Initialization-LifecycleEntity]]"
  - "[[Time]]"
  - "[[Feature-Items]]"
  - "[[Assets-Addressables]]"
tags:
  - architecture
  - daily-bonus
  - features
updated: 2026-08-26
---

# Feature-DailyBonus

## Для агента

Открывай эту статью, когда трогаешь ежедневный бонус — или когда пишешь **любую фичу, которая
нужна не каждый запуск**. DailyBonus — единственный в шаблоне пример `IConditionalEntity`, и его
устройство отвечает на вопрос «как не создавать окно, которое сегодня не нужно».

Три вещи, которые определяют всю фичу:

1. **Условие живёт в `ShouldRun()`, а не в `Init`.** Если награда сегодня уже забрана, фаза `Init`
   не вызывается вообще: попап не грузится, ассеты не тратятся.
2. **День считается по местному времени игрока** (`IClock.ServerLocalNow`), а не по UTC.
3. **Фича живёт одну сессию**: попап закрылся — `Dispose`, ассеты освобождены.

## Условие показа: IConditionalEntity

`DailyBonusCore` реализует `IConditionalEntity` (`DailyBonusCore.cs:28`). `LifecycleGate` вызывает
`ShouldRun()` **один раз до фаз**, когда конфиги, серверное время, сейв и post-inject уже готовы;
`false` гасит сущность целиком — ни `Load`, ни `Init`, ни обёртка `[AutoPopup]` не выполняются.
Механика гейта — [[Initialization-LifecycleEntity]].

`ShouldRun` здесь делает больше, чем читает флаг (`DailyBonusCore.cs:48-58`): он создаёт модель и
аналитику, фиксирует `_localNow` и прогоняет `Evaluate`. Побочные эффекты в `ShouldRun` **допустимы
по контракту** — и здесь они обязательны: потерянный streak нужно сбросить и отправить в аналитику
даже тогда, когда попап сегодня не показывается.

Отсюда же следствие для `Init`: ранние выходы и проверки на `null` в нём не нужны. Если `Init`
вызвался — условие уже выполнено.

Конфиг сильнее условия: при `is_enabled: false` в `DailyBonusConfig` `ShouldRun` не вызывается вовсе.

## Суточный сброс по местной полуночи

Дата последней награды хранится в `DailyBonusData.LastRewardDate` в **местном** времени игрока, и
это зафиксировано комментарием прямо в схеме (`DailyBonusData.cs:14-16`). Причина: день должен
сбрасываться в местную полночь. Если хранить UTC и сравнивать с UTC, для игрока в UTC+7 новый день
наступал бы в 07:00 по его часам.

Поэтому `Core` берёт `_clock.ServerLocalNow` — серверное время, сдвинутое в таймзону игрока
(см. [[Time]]). Не `DateTime.Now`: часы устройства игрок переводит сам, и в контракте `IClock` их нет.

Вся дальнейшая арифметика — по `.Date`, то есть по календарным дням, а не по 24-часовым интервалам
(`DailyBonusModel.cs:35-38,62-64`).

**Первый запуск.** `LastRewardDate == default` означает «награду ещё не брали». Модель ставит
«вчера» (`InitLastRewardDate`, `DailyBonusModel.cs:41-47`), чтобы сегодняшний попап показался, а
`UpdateStreak` не посчитал разрыв.

## Streak

`UpdateStreak` (`DailyBonusModel.cs:62-80`) различает три исхода, и `StreakUpdate` кодирует их
статическими фабриками (`None` / `Restarted` / `Lost(day)`):

| Прошло дней | Все награды собраны | Результат |
| --- | --- | --- |
| 0 | — | `None` — награда уже была сегодня |
| 1 | нет | `None` — обычное продолжение |
| 1 | да | `Restarted` — цикл пройден, начинаем заново |
| > 1 | — | `Lost(предыдущий день)` — streak потерян, уходит в аналитику |

`Evaluate` соблюдает порядок: сначала `InitLastRewardDate`, потом `UpdateStreak`, и только затем
решение о попапе (`DailyBonusModel.cs:54-60`). Порядок важен — сброшенный streak меняет ответ на
вопрос «есть ли конфиг для текущего дня», и этот инвариант закрыт тестом
`Evaluate_ResetsStreakBeforePopupDecision_WhenSeveralDaysMissed`.

Выдача награды идёт строго через `IInventory` и только после успешного `Add`
(`DailyBonusCore.cs:106-121`): не удалось начислить — `ClaimReward` не вызывается, дата и streak не
двигаются, и игрок получит попап снова. См. [[Feature-Items]].

Дни в конфиге сортируются по `StreakDay` один раз и кэшируются (`GetSortedDays`,
`DailyBonusModel.cs:101-104`) — порядок записей в JSON на логику не влияет.

## Попап и префабы дней

`[AutoPopup(DailyBonusConstants.Prefabs.Popup)]` загружает и регистрирует попап, см. [[UI-Views]].
А вот вид каждого дня внутри попапа — это **пять разных префабов**, выбираемых по состоянию:

| Префаб | Когда |
| --- | --- |
| `DailyBonusPreviousDay` | день уже пройден |
| `DailyBonusToday` | сегодняшний день |
| `DailyBonusTodayLastDay` | сегодняшний и он же последний в цикле |
| `DailyBonusNextDay` | будущий день |
| `DailyBonusLastDay` | будущий и последний в цикле |

Выбор делает чистая статическая функция `GetPrefabKey(streakDay, currentStreakDay, isLastDay)`
(`DailyBonusDayViewModelFactory.cs:65-88`) — `internal`, потому что её отдельно тестируют
(`DailyBonusViewKeyTests`, пять случаев). Пять префабов вместо одного с переключателями — легальный
случай вариантов одного типа: имя префаба здесь роль, а не имя компонента, см. [[Naming]].

Создание идёт в два шага и двумя разными объектами:

- `DailyBonusDayViewModelFactory` собирает VM: считает родителя из `IRewardRowLayout` (`Framework.Features.UI`), выбирает
  префаб, локализует подпись, тянет иконку из атласа.
- `DailyBonusDayViewSpawner` инстанцирует префабы **через собственный `IAssetScope`** и биндит VM
  (`DailyBonusDayViewSpawner.cs:18-25`).

Разделение не косметическое: спавнер — единственный, кто трогает ассеты, и именно поэтому владение
временем жизни выражается одной строкой в `Dispose`.

## Время жизни и владение ассетами

`DailyBonusCore` — редкий случай сущности, которая **умирает раньше сцены**:

```csharp
_popupView.SubscribeOnClosed(Dispose);
```

Попап закрылся — `Dispose` (`DailyBonusCore.cs:63`). В нём фича сбрасывает статусы, диспозит VM и
**свой** `IAssetScope` (`DailyBonusCore.cs:123-132`), а `base.Dispose()` снимает попап. Отписка от
`OnClosed` не нужна: шорткаты `SubscribeOn*` чистятся вместе с view.

Scope создаётся через `IAssetScopeFactory` — единственную ассет-зависимость, которую вправе
инжектить фича (см. [[Assets-Addressables]]). Префабы дней трекаются им, и `scope.Dispose()`
освобождает и инстансы, и ключи. Попап при этом принадлежит декоратору `[AutoPopup]` и своему
scope-у — руками его не трогают.

`SetInited(false)` в `Dispose` — осознанный ранний выход: он сообщает lifecycle, что сущность больше
не инициализирована, и такой явный отказ lifecycle не перебивает.

## Инварианты

- Условие показа живёт в `ShouldRun()`; в `Init` нет проверок «а надо ли показывать».
- `LastRewardDate` пишется и сравнивается только в местном времени (`ServerLocalNow`), никогда в UTC.
- Даты сравниваются по `.Date`, а не по разнице `TimeSpan`.
- `ClaimReward` вызывается **только** после успешного `IInventory.Add`.
- `Evaluate` обновляет streak до решения о попапе.
- `GetPrefabKey` не имеет побочных эффектов и покрыт тестами на все пять исходов.
- Каждый ключ из `DailyBonusConstants.Prefabs` есть в Addressables (`AddressableKeyTests`).
- Каждому `item_name` в `DailyBonusConfig.json` соответствует валюта из `CurrenciesConfig`, иначе
  `Add` вернёт `false` и награда не выдастся.
- `Dispose` освобождает `IAssetScope` фичи; префабы дней не остаются в памяти после закрытия попапа.
- В `DailyBonusData` новые сериализуемые члены добавляются только в конец (см. [[SaveLoad]]).

## Как расширять

**Другое число дней или другие награды** — только `DailyBonusConfig.json`: `streak_day`, `item_name`
(id валюты), `item_count`, `item_sprite`. Кода менять не нужно, порядок записей не важен.

**Повторный показ в тот же день** (кнопка «открыть бонусы»): фича сейчас одноразовая за сессию —
после `Dispose` её никто не поднимет. Открытие по действию игрока — это отдельная сущность или
переход попапа под `ViewRouter` с ручным `Open`, а не ослабление `ShouldRun`.

**Награда за неделю вперёд / оффлайн-компенсация**: считается в модели по `LastRewardDate`, данные
уже есть. Начислять несколько дней сразу нужно одним проходом с проверкой `TryGetCurrentDayConfig`
на каждый день — цикл в `Core`, а не в `Inventory`.

**Реакция на прыжок времени** (ресинк часов посреди сессии) сейчас отсутствует: состояние
оценивается один раз в `ShouldRun`. Понадобится — это payload-сигнал от `Clock`, см. [[Time]].

**Анимация получения**: во view и VM дня; модель и `Core` не трогаются.

## Тесты

`Assets/Framework/Features/Tests/`:

- `DailyBonusModelTests` — 17 тестов: инициализация даты, «сегодня уже забрал», решение о попапе,
  все ветки `UpdateStreak`, порядок в `Evaluate`, `ClaimReward`, границы конфига.
- `DailyBonusViewKeyTests` — пять исходов `GetPrefabKey`.

`DailyBonusCore` не тестируется: это composition root. Обратите внимание, что вся логика решения
живёт в `DailyBonusModel` и принимает `localNow` **параметром** — именно поэтому тесты обходятся
без фейка часов и без Unity, см. [[Testing-TDD]].

## Когда обновлять

- Изменилось условие показа или момент его вычисления.
- Изменились правила streak (`UpdateStreak`, `Evaluate`, `ClaimReward`).
- Сменилась шкала времени у `LastRewardDate` — это ломающее изменение сейва.
- Добавился или исчез префаб дня, изменилась логика `GetPrefabKey`.
- Изменилась схема `DailyBonusConfig.json`.
- Фича перестала быть одноразовой за сессию или сменила владение ассетами.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Feature-DailyBonus") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Feature-DailyBonus") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
