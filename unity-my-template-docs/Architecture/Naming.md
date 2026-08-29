---
title: Naming
type: architecture
area: Project
module: Naming
status: actual
source_paths:
  - Assets/Framework/Foundation/
  - Assets/Framework/Features/
  - Assets/Framework/Integrations/
  - Assets/AddressableAssetsData/AssetGroups/
  - Assets/Framework/Features/Tests/AddressableKeyTests.cs
  - Tools/naming-check.ps1
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[UI-MVVM]]"
  - "[[Testing-TDD]]"
  - "[[Assets-Addressables]]"
tags:
  - architecture
  - naming
  - conventions
  - foundation
  - features
  - assets
updated: 2026-08-26
---

# Naming

## Для агента

Открывай эту статью, когда выбираешь имя типа или ассета, вводишь новый термин или переименовываешь существующее. Конвенция покрывает и код, и имена, живущие вне `.cs`: файлы ассетов и ключи Addressables (секция «[Имена ассетов](#имена-ассетов)»). Это **единственный источник истины** по наименованию; секция «Наименование» в `AGENTS.md` — выжимка, при расхождении права у статьи.

### Три закона

1. **Имя = роль в системе.** Не базовый класс (`MonoBehaviourContainer` — нарушение), не паттерн реализации (`TargetFrameRateSetter` — нарушение), не способ, которым тип написан.
2. **Одно понятие — одно слово во всём шаблоне.** Если слово занято глоссарием (`Config`, `Core`, `Info`, `Data`, `Channel`, `Player`, `Source`), оно не может значить второе.
3. **Имя должно быть угадываемым.** Зная роль и домен, разработчик обязан назвать тип, не открывая папку. Если `Inventory` и `ItemCounter` не угадываются — конвенция сломана, а не разработчик.

### Порядок действий при выборе имени

См. «[Как выбрать имя](#как-выбрать-имя)» — четыре шага, выполнять по порядку.

## Инварианты

Формулировки сознательно машинные: каждая — правило `Tools/naming-check.ps1`.

- Ни один тип не оканчивается на `Manager`, `Helper`, `Utils`, `Handler`.
- `Controller` — **только** фасад подсистемы (единственная точка входа для фич).
- `Service` — **только** тонкий адаптер к внешней системе (ОС, SDK, сервер, файловая система).
- `Core` — **только** composition root фичи. Не слой, не сборка, не сцена.
- Атрибут объявляется с суффиксом `Attribute`: `class Foo : Attribute` без суффикса — ошибка.
- Сигнал: суффикс `Signal` обязателен, имя в прошедшем времени, префикс `On` запрещён.
- `I` ставится только на интерфейсе. Абстрактный класс — суффикс `Base`.
- Пустой интерфейс допустим, только если пустота и есть контракт: generic-constraint (`ISignal`), граница фичи поверх другого интерфейса (`IClickerCore : IEntityStatus`). Маркер «просто чтобы пометить» — атрибут. `naming-check` флагит все пустые интерфейсы, законные перечислены в `Tools/naming-check.exceptions.txt` с причиной. Причина в этом файле — утверждение о коде, и оно проверяется: тройка `I*SceneState` числилась там «ключом DI», никуда при этом не инжектясь, и уехала из шаблона целиком.
- namespace = путь от `Assets/` минус служебные сегменты. Список служебных сегментов закрыт: `Scripts`, `Content`, `Editor`. Любой другой сегмент пути обязан быть в namespace.
- Последний сегмент namespace не совпадает с именем ни одного типа внутри него.
- Один публичный тип = один файл, имя файла = имя типа.
- `ScriptableObject` не называется `*Config` — только `*Settings`.
- Ключ Addressables равен имени файла ассета без расширения.
- Запись Addressables адресует файл, а не папку.
- Имя файла `ScriptableObject`-ассета равно имени его типа.

## Суффиксы: открытый набор + два закрытых списка

**Базовое правило: суффикс — это точное существительное роли, любое.** `ItemCounter`, `SaveEnvelope`, `ConfigResolver`, `UnityLifecycleRelay`, `ButtonSoundBinder`, `FrameRateLimiter`, `RewardRowLayout`, `CsvParser` законны без внесения в какой-либо список — они называют роль одним словом.

> **Таблицы ниже не являются разрешённым набором.** Это два *ограничения* поверх открытого набора: что нельзя никогда и что уже занято. Отвергать имя со словами «такого суффикса нет в списке» — неверное прочтение статьи. Закрытая таблица суффиксов не работает: новая роль появляется чаще, чем правится статья.

### Список 1 — запрещены всегда

| Суффикс | Почему |
|---|---|
| `Manager` | не роль, а отказ выбрать роль; в шаблоне им были названы 16 разных вещей |
| `Helper` | то же, плюс притягивает несвязанный код в один тип |
| `Utils` | контейнер без границ |
| `Handler` | скрывает, что именно делает тип |

### Список 2 — зарезервированы: смысл зафиксирован и другим быть не может

| Суффикс | Зафиксированный смысл | Эталон |
|---|---|---|
| `Model` | доменная логика фичи | `ClickerModel` |
| `ViewModel` | состояние и команды для UI | `ClickerViewModel` |
| `View` | пассивный MonoBehaviour-презентер | `ClickerWindowView` |
| `Core` | **только** composition root фичи | `ClickerCore` |
| `Controller` | **фасад подсистемы** — единственная точка входа для фич | `AdsController`, `AudioController` |
| `Service` | **тонкий адаптер к внешней системе** (ОС, SDK, сервер) | `IAnalyticsService`, `FileService` |
| `Provider` | «дай по ключу», владеет кэшем/lifetime | `IAssetProvider`, `ConfigProvider` |
| `Factory` | создаёт новый инстанс **и возвращает его** | `DailyBonusDayViewModelFactory` |
| `Registry` | владеет коллекцией по ключу, отдаёт существующее | — |
| `Storage` | чтение/запись в конкретный бэкенд | `FileSaveStorage` |
| `Reader` / `Writer` | однонаправленный доступ к данным | `ConfigReader` |
| `Loader` | асинхронная загрузка ассета | `AudioClipLoader` |
| `Scope` | владение временем жизни (DI или ассеты) | `SceneScope`, `AssetScope` |
| `Signal` | факт в прошедшем времени; обязателен для всех `ISignal` | `AdStartedSignal` |
| `Config` | remote-конфиг фичи (`IConfig`, гейтит сущность) | `ClickerConfig` |
| `Settings` | ScriptableObject / локальные настройки | `TactileButtonAnimationSettings` |
| `Constants` | только `const` / `static readonly`, имя по домену | `ClickerConstants` |
| `Attribute` | атрибут — в объявлении всегда | `SaveTagAttribute` |
| `Tests` | имя SUT + `Tests` | `ClickerModelTests` |
| `Fake` *(префикс)* | ручной фейк для тестов | `FakeInventory` |
| `Base` *(суффикс)* | абстрактная база, когда интерфейс невозможен | `LoadingCurtainViewBase` |

### Условно разрешены — только в одном узком смысле

| Суффикс | Разрешено | Эталон |
|---|---|---|
| `Info` | immutable-снимок состояния | `CachedAssetInfo` |
| `Data` | конкретный сейв-блоб (не базовый тип) | `ClickerData` |
| `Wrapper` | когда обёртка и есть роль | `ViewWrapper` (internal) |
| `Spawner` | создаёт и размещает N объектов в сцене, ничего не возвращая | `DailyBonusDayViewSpawner` |

## Интерфейсы и реализации

- `I` + **имя роли**. Реализация без префикса — если она единственная и «главная»: `IViewRouter` → `ViewRouter`.
- Реализаций несколько → интерфейс по роли, реализация по бэкенду: `<Backend><Role>`. Эталон и лучший паттерн шаблона: `FileSaveStorage`, `PlayerPrefsSaveStorage`, `EditorAdsProvider`, `NullAdsProvider`.
- `I` никогда не ставится на класс. Абстрактная база — суффикс `Base` (`LoadingCurtainViewBase`).
- Интерфейс без членов и без роли не заводится: он ничего не обещает, но занимает лучшее имя в неймспейсе. Нужен маркер — атрибут. Исключение — случаи, где пустота и есть контракт: `ISignal` (generic-constraint шины), `IClickerCore : IEntityStatus` (граница фичи).
- Read-only-срез — `IReadOnly<X>`: `IReadOnlyEntityStatus` (эталон).
- Интерфейс для фиче-внутренних Model / ViewModel не заводится — только на границах фич (`ISettingsCore`).

## Слова, регистр, члены

- Тип — существительное **единственного** числа. Папка и namespace — множественное или доменное имя: `Views`, `Signals`, `Configs`. Папка, названная по своему главному классу, — источник коллизии «namespace == тип», поэтому такие папки переименовываются.
- namespace = путь от `Assets/` **с выброшенными служебными сегментами**. Список закрыт: `Scripts`, `Content`, `Editor`. Папки с этими именами на диске остаются (Unity-конвенция раскладки), в namespace они не попадают никогда.
- Аббревиатуры: 2 буквы — капсом (`UI`, `IO`), 3+ — PascalCase (`CsvTable`, `Json`, `Url`, `Api`). Свои сокращения запрещены.
- `bool` — `Is*` / `Has*` / `Can*` / `Should*`.
- Метод — глагол в императиве: `Load`, `TryGet`, `Release`. `TryX` обязан возвращать `bool` или `Result<T>`.
- Внутри владельца контекст не дублируется: в `ShopCore` — `_data`, а не `_shopData`.
- Поля: `m_PascalCase` для `[SerializeField]`, `_camelCase` для остальных private.
- Один публичный тип = один файл, имя файла = имя типа. Generic-перегрузка живёт в файле неарного типа (`MonoView.cs` содержит `MonoView` и `MonoView<T>`).

## Сигналы

Формула: `<Субъект><Событие в прошедшем времени>Signal`, без `On`.

```
AdStartedSignal, AdFinishedSignal, SceneStartedSignal, ApplicationPauseChangedSignal,
LoadingCurtainShownSignal, SceneLoadFailedSignal
```

- `On` принадлежит **обработчику** (`OnSceneStarted()`), не событию. Сигнал — это факт.
- Суффикс `Signal` сохраняется всегда, включая запросы: тип реализует `ISignal`, суффикс отражает контракт. Запрос выражается корнем — `SceneChangeRequestedSignal`.
- Многократно повторяющийся тик — не «событие началось»: сигнал с прогрессом называется `SceneLoadingProgressSignal`.
- Событие, у которого два направления (`bool` в payload), — `*ChangedSignal`: `ApplicationPauseChangedSignal`.

## Имена ассетов

Адрес Addressables **не участвует** в разрешении ссылок — ссылки идут по GUID. Поэтому связь «адрес ↔ файл ↔ тип» ничем, кроме конвенции, не держится и рассинхрон никогда не падает сам: он всплывает в рантайме в фазе `Load` или не всплывает вовсе, оставляя мёртвый адрес.

**Правило стиля: ключ Addressables = имя файла ассета, PascalCase.**

```
ClickerWindow, SettingsPopup, LoadingCurtain, DailyBonusTodayLastDay,
ClickerConfig, AdsConfig, CoreSceneMusic, WindowCanvas
```

- **Ключ, собираемый форматом из данных, наследует стиль данных** — это правило, а не исключение. `gem_icon_atlas` собирается из `IconConstants.Formats.AtlasName = "{0}_icon_atlas"`, где `{0}` — item id, а item id — ключ словаря в `ItemsData`. Смена регистра там стоит миграцию сейва через `CurrentVersion` / `Migrate`, то есть цена косметики — версия схемы. Такие адреса под правило PascalCase не подводить.
- **Ключ = имя файла; совпадение с именем типа не требуется.** Так легальны варианты одного типа без единой строки в исключениях: `DailyBonusToday` / `DailyBonusNextDay` / `DailyBonusLastDay` — шесть префабов одного `DailyBonusDayView`.
- **Имя файла `ScriptableObject`-ассета = имя типа** — следствие правила `*Settings`: `TactileButtonAnimationSettings.asset` рядом с `TactileButtonAnimationSettings.cs`.
- **Имя префаба = роль.** Совпадение с именем компонента не требуется; `UnityLifecycleRelay.prefab` держит `UnityLifecycleRelay` и `FrameRateLimiter` — имя по главной роли объекта, а не по списку компонентов.
- **Folder-entry в Addressables запрещён.** Адресуется конкретный ассет: у папки адрес — имя папки, а адреса вложенных ассетов пакет выводит из путей, и ни то, ни другое кодом не используется. Такая запись выглядит рабочей и не несёт нагрузки.
- **Папка ассетов фичи называется `Content/`** — не по типу Unity-объектов внутри (`ScriptableObjects/`, `Prefabs/` на верхнем уровне фичи). Имя папки утекает в адрес folder-entry и в путь, который видит агент.

### Вне конвенции

| Что | Почему |
|---|---|
| Сторонние киты: `Features/UI/Sprites/DefaultUI/**`, `Features/UI/Prefabs/Particles/**`, `Popup0*` | переименование ломает обновление кита |
| Ассеты, порождённые пакетами: Localization (`English (en)`, `General_ru`, `General Shared Data`), TMP (`RubikOne-Regular SDF`) | имя и адрес задаёт пакет |
| Группы Addressables, которые создаёт пакет (`Localization-*`) | группа принадлежит пакету; её записи он переписывает сам |

Переименование **файла** ассета безопасно всегда — `git mv` вместе с `.meta`. Переименование **адреса** ломает строковые константы и делается только парой с правкой кода.

## Глоссарий шаблона

| Термин | Значит ровно это | Не значит |
|---|---|---|
| **Config** | remote-конфиг фичи, гейтит сущность | ScriptableObject (это `Settings`) |
| **Settings** | локальные настройки в ScriptableObject | *(фича `Settings` — другой слой, коллизия допустима)* |
| **Blob** | секция сейва с тегом и версией | любые данные |
| **Foundation** | переиспользуемый слой и его сборка | ~~Core~~ |
| **Core** | composition root **фичи** | слой, сцена, сборка |
| **Lifecycle** | фазовая инициализация | ~~Control~~ |
| **Auto** | «фреймворк делает это за тебя» | ~~Fast~~ |
| **Window / Popup** | вид view — только в имени View и ключе префаба | не в Core / Model / ViewModel / Data |
| **Channel** | именованный поток логов (`LogChannel`) | не аудио-канал: аудио — `*Player` |
| **Player** | воспроизводит звук через `AudioSource` | не «игрок» — сущности игрока в шаблоне нет |
| **Source** | источник данных или тика (`IRealtimeSource`, `IRemoteConfigSource`) | не `AudioSource`-обёртка |

## Зафиксированные исключения

Решения, которые легко принять за недоделку и «дочистить». Каждое принято сознательно.

| Исключение | Причина |
|---|---|
| `IConditionalEntity` остаётся без `Lifecycle` | слова `Control` в имени нет; `ILifecycleCondition` потерял бы то, что интерфейс реализует сама сущность |
| Наследники `SaveBlob` остаются `*Data` (`ItemsData`, `ClickerData`, …) | суффикс `Data` для конкретного блоба конвенцией разрешён; имена наследников не врут, врал базовый тип |
| `Tools/fast-tests.ps1` сохраняет слово `fast` | оно про скорость прогона тестов, а не про декораторы `Fast*` |
| `ViewWrapper` сохраняет суффикс `Wrapper` | обёртка и есть роль типа; `internal` |
| Сцена `CoreScene`, `CoreSceneState`, `CoreSceneInitOrder`, `SceneConstants.Scenes.Core` | `Core` освобождён от смысла «сборка», коллизии больше нет; это имя сцены |
| Сцена `StartScene`, `StartSceneInitOrder`, `SceneStarter` | про сцену и её запуск, не про фичу `MainMenu` |
| `*SceneInitOrder` enum-ы не получают `Lifecycle` | они про сцену, а не про фазовую сущность |
| Свойство `Logger` в генерируемом коде | оно про роль внутри класса, а не про тип (`ILogChannel Logger`) |
| Addressables-ключ `SettingsPopup` | ключ про префаб, а не про фичу |
| Базовый VM указывается полным именем `Framework.Foundation.UI.Mvvm.ViewModel` | сегмент namespace `ViewModel` у фич (`Framework.Features.Clicker.ViewModel`) затеняет короткое имя типа. Удаление `.Scripts` из namespace этого не сняло — папка `ViewModel/` остаётся Unity-конвенцией раскладки |
| `ISignalBus` / `ReactiveSignalBus` в namespace `Framework.Foundation.Signals` | `Bus` — точное существительное роли; правило нарушалось бы только при namespace, равном имени типа, а он теперь `Signals` |
| Папка `UI/Views/` содержит и `ViewRouter`, и `ViewFactory` | обе — части одной подсистемы view; отдельная папка `ViewFactory/` давала namespace, совпадающий с именем типа |

## Эталоны

На эти имена смотреть, выбирая новое:

- `<Backend><Domain>Storage`: `FileSaveStorage`, `PlayerPrefsSaveStorage`, `FileConfigStorage`, `PlayerPrefsConfigStorage`;
- `AdsPolicy`, `PopupStack`, `WindowQueue`, `ViewOperationPump`, `ViewStateNotifier`, `ViewRegistration`, `ViewOperation`;
- `StopwatchRealtimeSource`, `LifecycleGate`, `SceneStarter`, `GameBootstrapper`, `AutoTypeScanner`, `ConfigTypeScanner`;
- `SaveReadResult` / `SaveReadStatus`, `Result<T>`, `IReadOnlyEntityStatus`, `AdFormat` / `AdResult`, `ClockTrust`;
- `DailyBonusDecision`, `StreakUpdate`, `ClickerTier`;
- триада `ClickerModel` / `ClickerViewModel` / `ClickerWindowView` — эталон MVVM-именования;
- `NullAdsProvider`, `EditorAdsProvider` — корректные имена заглушек.

## Антипримеры с разбором

Правило можно прочитать не так; разобранный случай — нет.

| Было | Стало | Почему |
|---|---|---|
| `MonoBehaviourContainer` | `UnityLifecycleRelay` | имя описывало базовый класс. Роль — трансляция Unity-колбэков (`Awake`/`Start`/`OnApplicationQuit`/`Pause`/`Focus`) в шину сигналов. Заодно ушёл namespace, совпадающий с именем типа |
| `ItemManager` + `ItemController` | `Inventory` + `ItemCounter` | два «суффикса по умолчанию» рядом: по имени невозможно предсказать, кто владелец коллекции, а кто счётчик одного item. Роли по коду — инвентарь (`Add`/`Remove`/`IsEnough`) и реактивный счётчик |
| `IItemManagerFactory.CreateManagers()` → `Dictionary<string, IItemController>` | `IItemCounterFactory.CreateAll()` | имя врало трижды: в типе, в методе и в типе результата. Переименование типа без пересмотра его контракта половинчато |
| `DtoManager` | `ConfigReader` | `Dto` означал не DTO, а remote-конфиг; `Manager` не описывал роль. Тип читает конфиг из источников — это `Reader` |
| `TargetFrameRateSetter` | `FrameRateLimiter` | «Setter» — это про свойство, а не про роль в системе |
| `ILoadingCurtainView` (abstract class) | `LoadingCurtainViewBase` | прямая ложь: `I` на классе. Причина существования абстрактного класса (MonoBehaviour за интерфейсом не сериализуется в Inspector) выражается суффиксом `Base` |
| `DailyBonusDayFactory` | `DailyBonusDayViewSpawner` | `CreateDayViews` ничего не возвращает — создаёт и биндит N view в сцене. Фабрика обязана возвращать созданное |
| `CurrencyViewContainer` | `CurrencyViewHostAttribute` | «Container» не говорит ничего. По коду это не полоса валют, а маркер-атрибут на классе view: «внутри есть дочерние `CurrencyView`, их надо проинжектить». Отсюда `Host` и обязательный суффикс `Attribute` |
| `ViewManager` | `ViewRouter` | `Manager` запрещён. Роль — регистрация view по ключу и навигация (`Open`/`Close`/`CloseAll`); `ViewController` не взят, потому что `Controller` в шаблоне значит «фасад подсистемы» и не появляется на UI-слое |
| `SceneLoadingStatusConfig` | `SceneLoadingProgress` | не ScriptableObject и не remote-конфиг, а изменяемое состояние прогресса загрузки, которое несёт `SceneLoadingProgressSignal` |
| `ItemConfig` | `ItemInfo` | не ScriptableObject и не remote-конфиг: неизменяемый снимок идентичности item-а (ключ, имя, описание, иконка) плюс read-only-значение счётчика |

## Как выбрать имя

1. **Назвать роль одним существительным.** Что этот тип делает в системе? Если ответ длиннее одного слова — вероятно, тип делает две вещи.
2. **Взять это существительное суффиксом.** Проверить по списку 1: не запрещено ли. Проверить по списку 2: если слово там есть, оно обязано значить ровно то, что там написано.
3. **Проверить глоссарий.** Не занято ли слово другим смыслом в шаблоне.
4. **Проверить угадываемость.** Смог бы я назвать этот тип так же, не видя файла, зная только его роль и домен? Если нет — вернуться к шагу 1.

## Проверка

Инварианты проверяет машина — `Tools/naming-check.ps1`. Скрипт только сообщает: ничего не правит и не форматирует. Одна связь скриптом не проверяется и живёт в EditMode-тесте `AddressableKeyTests` (`Assets/Framework/Features/Tests/`): «константа-ключ в коде → запись в Addressables» требует рефлексии, а не чтения YAML. Подробности — [[Assets-Addressables]].

```bash
powershell -File Tools/naming-check.ps1 -All
```

- без параметров — только изменения рабочего дерева относительно `HEAD`;
- `-Files <пути>` — явный список;
- `-BaseRef <ref>` — другая база сравнения;
- `-All` — полный проход по `Assets/Framework/**/*.cs`.

Exit 0 — тишина, exit 1 — есть находки. Прогон автоматизирован Stop-хуком (`Tools/hook-naming-check.ps1`, зарегистрирован в `.claude/settings.json`) и идёт **после** `fast-tests`: сначала важно знать, что код компилируется. Один и тот же набор находок блокирует ход один раз (хэш в `.agent-state/NamingCheck/.last-report`).

### Что проверяется

| Правило | Смысл |
|---|---|
| `forbidden-suffix` | тип оканчивается на `Manager` / `Helper` / `Utils` / `Handler` |
| `i-prefix-on-class` | `I` + заглавная в имени класса |
| `signal-on-prefix` | имя сигнала начинается с `On` |
| `signal-suffix` | тип реализует `ISignal`, но не назван `*Signal` |
| `attribute-suffix` | атрибут объявлен без суффикса `Attribute` |
| `empty-marker-interface` | интерфейс без единого члена |
| `scriptableobject-config` | `ScriptableObject` назван `*Config` вместо `*Settings` |
| `dead-term` | в коде остался `Dto`, `Fast*`, `ControlEntity` или `LogManager` |
| `injectable-ctor-missing-attribute` | рядом с `internal`-швом стоит публичный пустой ctor без `[Inject]` |
| `namespace-service-segment` | в namespace попал `Scripts` / `Content` / `Editor` |
| `namespace-path-mismatch` | namespace не равен пути от `Assets/` минус служебные сегменты |
| `namespace-equals-type` | последний сегмент namespace совпадает с именем типа внутри |
| `file-name-mismatch` | в файле нет типа верхнего уровня с именем файла |
| `multiple-public-types` | **предупреждение**: несколько публичных типов верхнего уровня в файле |
| `addressable-address-mismatch` | адрес записи Addressables не равен имени файла ассета |
| `addressable-folder-entry` | запись Addressables адресует папку, а не файл |
| `scriptableobject-file-type-mismatch` | имя файла `*.asset` не равно имени его `m_Script` |

`injectable-ctor-missing-attribute` — единственное правило скрипта не про имя: оно про пару конструкторов «`[Inject]` + `internal`-шов» (описание — [[Testing-TDD]]). Живёт здесь потому, что проверка та же по природе — разбор исходника без компиляции, — а отдельный скрипт ради одного правила размножил бы Stop-хуки. Правило молчит, пока в типе нет `internal`-ctor с параметрами.

Последние три правила читают ассеты, а не `.cs`, и работают через индекс `guid → путь`, построенный по всем `.meta` в `Assets/` (~1700 файлов, ~1 с). Полный проход по `.meta` — единственный способ развернуть GUID вне редактора, поэтому правила включаются не всегда:

- `-All` — всегда;
- diff- и `-Files`-режимы — только если в списке изменений есть `.asset` или `.meta` под `Assets/`. Триггером служит `.meta`: он переезжает вместе с любым переименованным ассетом, какого бы типа тот ни был, а сам ассет при переименовании не меняется.

Отсечения, которые правила делают молча, без строк в исключениях:

- запись Addressables, чей GUID не разворачивается — ассет живёт вне `Assets/` (пакет) либо запись висячая; второе — не про имена;
- запись, чей ассет лежит вне `Assets/Framework` — пакетные и сторонние ассеты;
- `*.asset`, чей `m_Script` не резолвится в `.cs` под `Assets/Framework` — так отсекаются все `ScriptableObject`, порождённые пакетами (Localization, TMP).

Исключение для правил по ассетам пишется по имени файла группы (`Localization-Assets-Shared`), по адресу или по имени файла ассета — тем же форматом `<токен> # <причина>`.

### Как добавить исключение

`Tools/naming-check.exceptions.txt`, формат `<токен> # <причина>`. Токен — имя типа, имя файла без `.cs` или полный namespace. **Причина обязательна**: строка без неё считается ошибкой скрипта, а не разрешением. Исключение выключает все правила для этого токена, поэтому пишется точечно и с объяснением, почему конвенция здесь неприменима, — иначе файл превращается в свалку и конвенция размывается второй раз.

## Когда обновлять

- Вводится новый суффикс, который претендует на фиксированный смысл → добавить в список 2.
- Вводится новый термин домена → добавить в глоссарий с колонкой «не значит».
- Обнаружен случай, который таблицы не покрывают → разобрать его в «Антипримерах», а не расширять список запретов.
- Принято сознательное отступление от конвенции → строка в «Зафиксированных исключениях» с причиной.

- Меняется набор классов-держателей ключей ассетов → синхронизировать `KeyHolderNames` в `AddressableKeyTests`.

## Last Verified

2026-08-05, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Naming") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Naming") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```