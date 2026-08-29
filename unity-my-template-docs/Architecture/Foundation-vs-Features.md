---
title: Foundation vs Features
type: architecture
area: Project
module: Assembly Boundaries
status: actual
source_paths:
  - Assets/Framework/Foundation/Foundation.asmdef
  - Assets/Framework/Features/Features.asmdef
  - Assets/Framework/Foundation/
  - Assets/Framework/Features/
  - Assets/Framework/Integrations/
related:
  - "[[Initialization-LifecycleEntity]]"
  - "[[UI-Views]]"
  - "[[Add-UI-Window]]"
  - "[[Assets-Addressables]]"
  - "[[Ads]]"
tags:
  - architecture
  - foundation
  - features
  - integrations
  - asmdef
updated: 2026-08-26
---

# Foundation vs Features

## Для агента

Используй эту статью перед добавлением новой фичи, сервиса, UI или данных. Главное правило: `Features` может зависеть от `Foundation`, но `Foundation` не должен зависеть от `Features`.

Если функциональность переиспользуемая между проектами, размещай её в `Assets/Framework/Foundation/`. Если она описывает конкретную игру или шаблонную игровую фичу, размещай её в `Assets/Framework/Features/`.

Имена сборок совпадают с именами файлов asmdef и со слоями: `Foundation` и `Features`. Слово `Core` за сборкой больше не закреплено — по конвенции наименования оно значит только composition root фичи (`ClickerCore`) и сцену `CoreScene`.

## Назначение

Проект разделён на общий корень `Assets/Framework/`, два основных слоя и изолированные адаптеры:

- `Assets/Framework/Foundation/` — переиспользуемый фреймворк проекта.
- `Assets/Framework/Features/` — конкретные игровые фичи и надстройки над `Foundation`.
- `Assets/Framework/Integrations/` — отдельные asmdef-адаптеры между `Foundation` и сторонними пакетами.

`Assets/Framework/Features/Features.asmdef` (сборка `Features`) ссылается на `Assets/Framework/Foundation/Foundation.asmdef` (сборка `Foundation`). Обратную зависимость вводить нельзя.

Integration-сборки могут ссылаться на `Foundation` и конкретный сторонний пакет. `Foundation` не должен
ссылаться ни на integration-сборку, ни на пакет, нужный только этому адаптеру.

## Что относится к Foundation

В `Foundation` живут инфраструктурные и переиспользуемые подсистемы:

- инициализация, scene lifecycle, scope-ы;
- DI-расширения и автоматическая регистрация `LifecycleEntity`;
- `ViewRouter`, canvas-ы, view factory, UI-анимации;
- `SignalBus`;
- save/load и базовый `SaveBlob`;
- asset loading — [[Assets-Addressables]];
- localization;
- audio-инфраструктура;
- logging;
- общие utilities и контракты UI-инфраструктуры (`IViewSetupStep`). Контракты **игрового** UI —
  в `Features`: `IRewardRowLayout` вместе с `RewardRowLayout` живёт в `Features/UI`, потому что
  ни один тип `Foundation` его не знает.

Код в `Foundation` не должен знать о конкретных игровых фичах вроде `Clicker`, `DailyBonus`, `MainMenu` или `SettingsPopup`.

Готовых игровых MonoBehaviour-ов и layout-ов в `Foundation` быть не должно: HUD валюты (`CurrencyView`, `CurrencyViewHostAttribute`, `CurrencyViewSetupStep`) и раскладка строк наград (`RewardRowLayout`) живут в `Features`, чтобы новый проект на шаблоне не наследовал чужой UI.

Экономика игры тоже не инфраструктура: движок предметов (`IInventory`, `Inventory`, `ItemCounter`, `ItemsData`, `CurrenciesConfig`) целиком лежит в `Features/Items/`. Раньше он был разрезан посередине — движок в `Foundation`, его же UI в `Features`; разрез означал, что схема сейва конкретной игры уезжает в общий upstream слоя.

## Что относится к Features

В `Features` живут игровые фичи и проектная сборка шаблона:

- игровые окна и popup-ы;
- gameplay-механики;
- игровые `*Data` (`SaveBlob`-наследники);
- игровые `Scope`-надстройки;
- константы префабов и ключей конкретной фичи;
- связка нескольких foundation-сервисов под конкретный сценарий.

Примеры текущих фич:

- `Assets/Framework/Features/MainMenu/`
- `Assets/Framework/Features/Settings/`
- `Assets/Framework/Features/Clicker/`
- `Assets/Framework/Features/DailyBonus/`
- `Assets/Framework/Features/Items/` — движок предметов и валют (`Scripts/`), его UI (`Scripts/View/`) и контент айтемов
- `Assets/Framework/Features/UI/` — общий игровой UI шаблона (`Scripts/RewardRowLayout.cs`, префабы, шрифты)

## Что относится к Integrations

В `Integrations` живёт только клей к опциональным сторонним пакетам: адаптер зависит от
`Foundation` и от конкретного пакета, а `Foundation` не знает ни о нём, ни о пакете. Сейчас папка
пуста — единственный её обитатель уехал вместе с платным SRDebugger ([[SRDebugger]]).

Готовые точки расширения `Foundation` под такие адаптеры:

- `IAnalyticsService` — аналитическая сеть; их может быть несколько одновременно (коллекция).
- `IAdsProvider` — рекламная сеть; активная всегда одна, выбирает `AdsScopeRegistrator` (см. [[Ads]]).
- `IRemoteConfigSource` / `IServerTimeSource` — LiveOps-бэкенд, выбирается `LiveOpsScopeRegistrator`.

## Инварианты

- `Foundation` не ссылается на namespace `Framework.Features.*`.
- Сборка `Foundation` не должна зависеть от сборки `Features`.
- Сборка `Foundation` не должна зависеть от asmdef из `Integrations` или от их сторонних пакетов.
- Общий паттерн или инфраструктура сначала проектируются как `Foundation`.
- Конкретная игровая механика остаётся в `Features`.
- Платных плагинов в репозитории нет ни в одном слое. Шаблон едет в другие проекты через subtree, и вторая игра не должна упираться в чужую лицензию. Кнопка «вызвать метод из инспектора» — штатный `[ContextMenu]` (`CanvasConfigurator`, `PopupObjectJumpAnimation`), подсказки — `[Header]` / `[Tooltip]` (`RewardRowLayout`); если штатного не хватает — кастомный `PropertyDrawer` в Editor-сборке.
- `.csproj` и `.sln` генерируются Unity и руками не редактируются.

## Как принимать решение

Задай вопрос: "Можно ли перенести этот код в другой проект без знания текущей игры?"

Если да — это кандидат в `Foundation`.

Если нет — это `Features`.

Пограничные случаи:

- UI-инфраструктура, canvas, `ViewRouter` — `Foundation`.
- Конкретное окно с кнопками, текстами и переходами — `Features`.
- Базовый save/load механизм — `Foundation`.
- Конкретные данные фичи — `Features`.
- Универсальный item/currency engine — `Features` (`Features/Items/`).
- Баланс или прогресс конкретной механики — `Features`.
- Готовый HUD валюты или layout наград — `Features`; их контракты — `Foundation`.

## Как расширять

Новая игровая фича добавляется в `Assets/Framework/Features/<FeatureName>/` и использует готовые сервисы из `Foundation`.

Новая переиспользуемая подсистема добавляется в `Assets/Framework/Foundation/<ModuleName>/`, если она не знает о конкретных игровых фичах. Если подсистеме нужен игровой адаптер, общий контракт оставь в `Foundation`, а адаптер размести в `Features`.

Адаптер к стороннему пакету размещай в `Assets/Framework/Integrations/<PackageName>/` с отдельным
asmdef. Он может зависеть от `Foundation`; обратная ссылка запрещена.

Если изменение требует зависимости `Foundation` от `Features`, это сигнал пересмотреть границу: чаще всего нужен интерфейс, сигнал или перенос конкретной логики в `Features`.

## Когда обновлять

Обнови эту статью, если:

- появляется новый asmdef в основных слоях проекта;
- меняется направление зависимостей;
- крупная подсистема переносится между `Foundation` и `Features`;
- появляется новый устойчивый слой, например `Editor`, `Tools` или отдельный пакет.

## Last Verified

2026-07-27, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Foundation-vs-Features") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Foundation-vs-Features") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```