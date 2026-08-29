---
title: SaveLoad
type: architecture
area: Foundation
module: SaveLoad
status: actual
source_paths:
  - Assets/Framework/Foundation/SaveLoad/SaveBlob.cs
  - Assets/Framework/Foundation/SaveLoad/SaveTagAttribute.cs
  - Assets/Framework/Foundation/SaveLoad/SaveEnvelope.cs
  - Assets/Framework/Foundation/SaveLoad/SaveLoadService.cs
  - Assets/Framework/Foundation/SaveLoad/ProgressSaver.cs
  - Assets/Framework/Foundation/SaveLoad/ISaveStorage.cs
  - Assets/Framework/Foundation/SaveLoad/SaveReadResult.cs
  - Assets/Framework/Foundation/SaveLoad/FileSaveStorage.cs
  - Assets/Framework/Foundation/SaveLoad/PlayerPrefsSaveStorage.cs
  - Assets/Framework/Foundation/SaveLoad/FoundationSaveTags.cs
  - Assets/Framework/Foundation/SaveLoad/Serialization/SaveLoadBootstrap.cs
  - Assets/Framework/Foundation/SaveLoad/Editor/SaveLoadMenu.cs
  - Assets/Framework/Foundation/File/ByteArrayFileStore.cs
  - Assets/Scripts/YandexGames/YandexLifecycleRelay.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
tags:
  - architecture
  - foundation
  - saveload
  - memorypack
updated: 2026-08-17
---

# SaveLoad

## Для агента

Используй эту статью, если нужно добавить новый save-data класс, новый storage-бэкенд, кастомный MemoryPack-форматтер или разобраться с форматом файла сейва.

Ключевое правило эволюции схемы: MemoryPack используется в дефолтном режиме (не `VersionTolerant`), поэтому **новые члены в `SaveBlob`-классы добавляются только в конец объявления**. Удалять и переставлять сериализуемые члены нельзя — это молча ломает загрузку старых сейвов.

Всё, что нельзя выразить добавлением в конец (переименование, смена типа, перенос значения), делается через версию схемы: поднять `SaveBlob.CurrentVersion` и написать перенос в `SaveBlob.Migrate(fromVersion)`. Старый член при этом остаётся в классе (на своём месте, можно пометить `[Obsolete]`) — он нужен, чтобы прочитать старый payload; удалять его нельзя.

Если удалить член всё-таки нужно (данные больше не нужны, мигрировать не из чего), это делается **сознательным сбросом блоба**: поднять `CurrentVersion` и вместе с ней `MinReadableVersion` до той же величины. Payload старее рубежа не десериализуется вовсе — блоб получает `PrepareNewData()`, остальной сейв читается как обычно. Без поднятого рубежа MemoryPack бросит `ThrowInvalidPropertyCount` (в заголовке объекта лежит число членов), и блоб будет сброшен с `LogError`.

## Назначение

Система сохранений: набор независимых `SaveBlob`-блобов (по одному на фичу/подсистему), сериализуемых MemoryPack-ом в один бинарный файл. Загрузка — in-place в уже созданные DI-инстансы, поэтому ссылки на `SaveBlob`, полученные через DI, переживают загрузку.

`SaveBlob` хранит только простые сериализуемые значения (примитивы, строки, коллекции). Реактивные обёртки (`ReactiveProperty<T>`) живут в рантайм-слое над `SaveBlob` — например `ItemCounter` держит `ReactiveProperty<BigInteger>`, пишет в `ItemsData` и отдаёт наружу `ReadOnlyReactiveProperty<BigInteger>`. Иначе формат сейва зависел бы от внутреннего устройства типа из R3, а обновление пакета молча ломало бы сейвы игроков.

## Ключевые типы

- `SaveBlob` — abstract база сериализуемого состояния; наследник обязан быть `[MemoryPackable] partial` и иметь `[SaveTag(ushort)]`. `virtual ushort CurrentVersion` (дефолт `1`) — версия схемы блоба, `virtual ushort MinReadableVersion` (дефолт `1`) — нижний рубеж читаемого payload-а, `virtual void Migrate(ushort fromVersion)` — точка переноса данных со старой версии.
- `SaveTagAttribute` — стабильный ushort-тег блоба в файле сейва; теги фиксируются навсегда.
- `SaveEnvelope` — собирает все `SaveBlob` из DI, сериализует/десериализует конверт.
- `SaveLoadService` (`LifecycleEntity`, фаза `Load`) — грузит сейв на старте, коалесит конкурентные `SaveData()`; `SaveDataImmediate()` синхронно сериализует и пишет финальный сейв при выходе.
- `ProgressSaver` — запускает автосейв непосредственно в `IStartable.Start()`: таймер 15 с на R3 с инжектируемым `TimeProvider` (регистрируется в `RootScope`; в плеере это Unity-провайдер R3). Ручные триггеры независимы от автосейв-петли: смена сцены — обычный `SaveData()`, pause и quit — синхронный `SaveDataImmediate()`. В тестах время двигается `FakeTimeProvider.Advance`.
- `ISaveStorage` — бэкенд: `FileSaveStorage` (файл) или `PlayerPrefsSaveStorage` (Base64 в PlayerPrefs, dev-фолбэк); выбирается define-ами в partial-регистрации scope-а. Обычная запись асинхронная, pause/quit-запись синхронная (`Write`); чтение возвращает `SaveReadResult` со статусом `Empty`, `Success` или `Corrupted`. Payload в результате — `ReadOnlyMemory<byte>`, а не `byte[]`: `ISaveEnvelope.Deserialize` принимает `ReadOnlySpan<byte>`, поэтому окно поверх прочитанного массива обходится без копии и не отдаёт наружу изменяемый буфер (см. [[Class-Interaction]]).
- `SaveLoadBootstrap` — регистрация кастомных MemoryPack-форматтеров до первого использования.

## Формат файла

Конверт пишется little-endian:

```
int32  count
далее count раз:
  uint16 tag
  uint16 version      // SaveBlob.CurrentVersion на момент записи
  int32  payloadLength
  byte[] payload      // MemoryPack-блоб одного SaveBlob
```

Неизвестный тег при загрузке пропускается по length (форвард-совместимость), отсутствующий в файле `SaveBlob` получает `PrepareNewData()`.

Версия читается до десериализации:

- `version == CurrentVersion` — обычная загрузка;
- `version in [MinReadableVersion, CurrentVersion)` — загрузка, затем `Migrate(version)`;
- `version < MinReadableVersion` — payload не читается: `PrepareNewData()` этого блоба + информационный лог (сознательный сброс схемы, не ошибка);
- `version > CurrentVersion` (сейв из будущей сборки) — `InvalidOperationException`, то есть карантин целиком; тихо обнулять прогресс нельзя.

Сериализация и парсинг конверта — без промежуточных копий: `ArrayBufferWriter<byte>` + `BinaryPrimitives` + span-слайсы; MemoryPack пишет payload напрямую в общий буфер через overload `Serialize(Type, IBufferWriter, object)`.

## Надёжность

- Запись файла атомарна: `ByteArrayFileStore.Save` пишет в `*.tmp` и подменяет через `File.Replace`, предыдущая версия остаётся в `*.bak`.
- Битый сейв не роняет запуск: storage возвращает `Corrupted` для повреждённого транспортного payload (например невалидного Base64), а `SaveLoadService.LoadAssets` дополнительно ловит исключение десериализации. Оба пути зовут `ISaveStorage.QuarantineAsync()` (файл переименовывается в `*.corrupted`, ключ PlayerPrefs копируется в `*.corrupted`-ключ) и продолжают с `PrepareNewData()`.
- **На web синхронный сейв держится на SDK платформы, а не на Unity.** `OnApplicationQuit` на web не вызывается вовсе (Unity-доки: "The Web platform doesn't support OnApplicationQuit because of the way browser tabs close"), поэтому `ApplicationQuittingSignal` там мёртв. Уход со страницы даёт `YG2.onPauseGame`, и `YandexLifecycleRelay` (`Assets/Scripts/YandexGames/`) транслирует его в `ApplicationPauseChangedSignal` — дальше работает обычный pause-путь `ProgressSaver`. Без этого relay web терял бы всё с последнего автосейва, тем более что интервал автосейва идёт по масштабируемому времени (`UnityTimeProvider.Update`), а YG2 на паузе и на interstitial ставит `Time.timeScale = 0`. Порт на другую web-платформу обязан завести свой relay: общего Unity-коллбека для этого нет.
- Сбой одного блоба изолирован: длина payload-а известна из конверта, поэтому `SaveEnvelope.TryLoadBlob` ловит исключение MemoryPack, зовёт `PrepareNewData()` **только этого** `SaveBlob`, пишет `LogError` и читает остальные блобы дальше. Сломанная схема одной фичи не стоит игроку валюты, прогресса и стриков. Карантин файла остаётся для сбоя самого конверта (обрезанный файл, битые длины) и для payload-а из будущей сборки.

## Editor-инструменты

Меню `Raycast Productions/Data/` (`SaveLoadMenu`, asmdef `SaveLoad.Editor`):

- `Open Folder` — открывает `persistentDataPath/Data/`.
- `Clean All` — удаляет файлы сейва и конфигов, чистит `PlayerPrefs` (`DeleteAll` + обязательный `Save()`: без него удаление живёт только в памяти процесса до выхода из редактора).

Где физически лежит сейв — зависит от define, и это первое, что нужно проверять при «чистка не сработала»:

- `FILE_SAVE_ENABLED` — `persistentDataPath/Data/SaveFile.bin`;
- `PLAYER_PREFS_SAVE_ENABLED` — ключ `SaveFile.bin` в PlayerPrefs; в редакторе на Windows это реестр `HKCU\Software\Unity\UnityEditor\<Company>\<Product>`, в плеере — `HKCU\Software\<Company>\<Product>`.

Чистка диска — только половина работы: `SaveEnvelope` держит блобы в памяти, и `ProgressSaver` вернёт их на диск ближайшим автосейвом, сменой сцены или записью на выходе из Play. Поэтому в Play mode `Clean All` спрашивает подтверждение и, получив его, сначала резолвит `ISaveEnvelope` через `LifetimeScope.Find<RootScope>()` и зовёт `PrepareNewData()`, и только потом выходит из Play — quit-сейв записывает уже пустой конверт. Порядок обратный (сначала выход, потом сброс) вернул бы прогресс.

## Как добавить новый SaveBlob

1. Класс в фиче/подсистеме:

```csharp
[SaveTag(FeaturesSaveTags.SomeFeatureData)]
[MemoryPackable]
public partial class SomeFeatureData : SaveBlob
{
    public int Level { get; internal set; }

    public override void PrepareNewData()
    {
        Level = 1;
    }
}
```

2. Тег — новая константа в `FoundationSaveTags` (диапазон 1..99) или в `FeaturesSaveTags` (100..199); значение не переиспользовать даже после переезда блоба. Занято: foundation — `AdsData = 2` (1 выведен из обращения вместе с уехавшим в `Features` `ItemsData`); features — `SettingsData = 100`, `DailyBonusData = 101`, `ClickerData = 102`, `ItemsData = 103`.
3. Регистрация не нужна: все конкретные наследники `SaveBlob` регистрируются автоматически (`RegisterAutoTypes` в `RootScope`, singleton, как `SaveBlob` и как сам тип).
4. Новые члены добавлять только в конец класса (см. «Для агента»).

## Как изменить схему существующего SaveBlob

```csharp
[SaveTag(FeaturesSaveTags.SomeFeatureData)]
[MemoryPackable]
public partial class SomeFeatureData : SaveBlob
{
    public int Level { get; set; }        // v1: остаётся в классе ради старых сейвов
    public int Experience { get; set; }   // v2: добавлен в конец

    public override ushort CurrentVersion => 2;

    public override void Migrate(ushort fromVersion)
    {
        if (fromVersion < 2)
        {
            Experience = Level * 100;
        }
    }
}
```

`Migrate` вызывается один раз после десериализации, ветки пишутся по возрастанию версий (`< 2`, `< 3`, …), чтобы сейв, перепрыгнувший несколько версий, прошёл все переносы подряд.

## Кастомные форматтеры

Типы без встроенной поддержки MemoryPack регистрируются в `SaveLoadBootstrap.Init` — `MemoryPackFormatterProvider.Register(new BigIntegerFormatter())`. Сейчас это единственный кастомный форматтер: `BigInteger` пишется как `byte[]` из `ToByteArray()`.

Регистрация обязана произойти до первого использования типа, поэтому `Init` помечен `[RuntimeInitializeOnLoadMethod(BeforeSplashScreen)]`; EditMode-тесты зовут его сами из `OneTimeSetUp`.

## Инварианты

- Запуск автосейва не зависит от одноразовых lifecycle-сигналов Unity: ранний сигнал мог бы прийти до регистрации подписчика и потеряться.
- Pause и quit используют синхронный storage-путь (`SaveDataImmediate`): на мобильных процесс может умереть, не дождавшись async-записи. Оба идут мимо автосейв-таймера.
- После начала immediate-записи storage отбрасывает старые отложенные async-записи, чтобы они не перезаписали финальный сейв — и файловый (`_immediateWriteStarted` под `lock`), и PlayerPrefs.
- Каждая запись PlayerPrefs (и `Write`, и `WriteAsync`, и `QuarantineAsync`) заканчивается `PlayerPrefs.Save()`: без него payload остаётся в памяти и теряется при kill из фона.
- Каждый `SaveBlob`-наследник имеет уникальный, навсегда стабильный `[SaveTag]`; `SaveEnvelope` бросает исключение на дубликат или отсутствие тега.
- Десериализация обязана переиспользовать DI-инстанс (`ref`-overload MemoryPack); `SaveEnvelope` бросает исключение, если инстанс подменён.
- Новые сериализуемые члены — только в конец класса; удаление и перестановка возможны лишь через поднятый `MinReadableVersion`, то есть с явным сбросом блоба (дефолтный режим MemoryPack).
- `MinReadableVersion <= CurrentVersion`: иначе схема не читает даже собственный payload — `SaveEnvelope` бросает исключение при индексации тегов.
- Сброс или потеря блоба всегда попадает в лог: сознательный (`version < MinReadableVersion`) — как `Log`, аварийный (исключение десериализации) — как `LogError`. Молчаливой потери прогресса быть не должно.
- В `SaveBlob` не попадают рантайм-механизмы уведомлений: `ReactiveProperty<T>` и прочие типы из R3 остаются в слое над `SaveBlob`, иначе формат сейва привязывается к версии пакета.
- Изменение смысла или расположения данных внутри `SaveBlob` обязано сопровождаться инкрементом `CurrentVersion` и веткой в `Migrate`; старые члены остаются в классе как источник для миграции.
- Payload с версией выше `CurrentVersion` не читается: карантин, а не `PrepareNewData` под видом успешной загрузки.
- `Serialize()` зовётся только на главном потоке (данные мутируются на нём); на thread pool уходит только запись байтов.
- Формат конверта менять нельзя без миграции существующих сейвов.
- `Empty` означает отсутствие слота; существующий, но нечитаемый payload обязан возвращать `Corrupted`.

## Частые ошибки

- Добавить `SaveBlob` без `[SaveTag]` или с чужим тегом — исключение при старте.
- Вставить новое поле в середину `[MemoryPackable]`-класса — старые сейвы читаются в неверные поля.
- Переименовать поле или сменить его тип без инкремента `CurrentVersion` — миграция не вызовется, данные молча уедут.
- Удалить старый член после написания `Migrate` — мигрировать станет не из чего.
- Удалить член, не подняв `MinReadableVersion` — блоб сбросится с `LogError` вместо штатного сброса; на схемах без версии это выглядит как «сейв побился».
- Зарегистрировать форматтер после первого использования типа — MemoryPack уже закэшировал отсутствие.

## Когда обновлять

Обнови эту статью, если меняются:

- формат конверта или способ его записи/чтения;
- контракт `SaveBlob` / `SaveTagAttribute` / `ISaveEnvelope` / `ISaveStorage`;
- политика версионирования (переход на `VersionTolerant` и т.п.);
- поведение автосейва (`ProgressSaver`) или коалесинга (`SaveLoadService`);
- источник lifecycle-событий на платформе без Unity-коллбеков (`YandexLifecycleRelay` и его аналоги);
- атомарность записи или карантин битых сейвов;
- способ регистрации форматтеров;
- поведение editor-меню `Raycast Productions/Data/` или место хранения сейва под конкретным define.

## Last Verified

2026-08-09, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "SaveLoad") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "SaveLoad") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```