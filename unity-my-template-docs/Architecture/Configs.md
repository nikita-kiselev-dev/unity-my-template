---
title: Configs
type: architecture
area: Foundation
module: Configs
status: actual
source_paths:
  - Assets/Framework/Foundation/Configs/
  - Assets/Framework/Foundation/Initialization/Scripts/Extensions/VContainerBuilderExtensions.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Registrators/Data/
related:
  - "[[Initialization-LifecycleEntity]]"
  - "[[SaveLoad]]"
  - "[[LiveOps]]"
  - "[[Assets-Addressables]]"
tags:
  - architecture
  - configs
  - foundation
updated: 2026-08-08
---

# Configs

## Для агента

Открывай эту статью, когда фиче нужны настраиваемые числа (цены, длительности, тумблер фичи) или
когда конфиг не загрузился.

Добавление конфига — три шага и ни одной регистрации:

1. Класс, реализующий `IConfig`, с `[ConfigKey("Key")]`.
2. Dummy-json по тому же ключу в Addressables.
3. `[Inject] private readonly MyConfig _config;` у потребителя.

Конфиг инжектится **как обычная зависимость**, синхронно. Никакого `await`, никакой фазы загрузки
у потребителя — к моменту его создания все конфиги уже в памяти.

Тумблер `IsEnabled` не проверяется руками: `LifecycleGate` гасит сущность целиком ещё до фаз —
см. [[Initialization-LifecycleEntity]].

## Назначение

Задача подсистемы — сделать конфиг обычным полем, хотя его источник асинхронный и ненадёжный.
Решается это переносом всей асинхронности в одну точку: **все** конфиги грузятся одним `WarmUp` до
построения потребителей, дальше в системе есть только словарь готовых объектов.

Почему не «загрузить в фазе `Load` той сущности, которой нужен конфиг»: `[Inject]` синхронный, а на
Bootstrap-сцене потребители конфигов уже существуют (`Inventory` из `Features`). Вставить `await`
между построением scope и созданием объектов негде.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `IConfig` | контракт конфига: единственный член — `bool IsEnabled` |
| `ConfigKeyAttribute` | ключ конфига на самом типе |
| `ConfigTypeScanner` / `ConfigTypeEntry` | скан сборок, пары «тип → ключ» |
| `IConfigProvider` / `ConfigProvider` | `WarmUp` + словарь готовых конфигов, Singleton |
| `IConfigReader` / `ConfigReader` | одно чтение: кэш, dummy-ассет, подписка на LiveOps |
| `IConfigResolver` / `ConfigResolver` | выбор источника и десериализация |
| `IConfigStorage` | кэш серверных значений: `FileConfigStorage` или `PlayerPrefsConfigStorage` |

Формат конфига — JSON через Newtonsoft, с `[JsonObject(MemberSerialization.OptIn)]` и
`snake_case`-именами в `[JsonProperty]`:

```csharp
[Serializable]
[JsonObject(MemberSerialization.OptIn)]
[ConfigKey(ClickerConstants.Configs.Key)]
public class ClickerConfig : IClickerConfig
{
    [JsonProperty("is_enabled")] private bool _isEnabled;
    [JsonProperty("clicker_levels")] private ClickerLevelConfig[] _levels;

    public bool IsEnabled => _isEnabled;
    public IReadOnlyList<ClickerLevelConfig> Levels => _levels;
}
```

Поля приватные, наружу — только readonly-свойства: конфиг immutable для всех, кроме десериализатора.
Коллекция наружу отдаётся как `IReadOnlyList`, а не массивом: массив читается как разделяемое
изменяемое состояние, и правило `mutable-state-exposed` из [[Class-Interaction]] его ловит. Поле
внутри остаётся массивом — Newtonsoft пишет именно в него.
Интерфейс поверх (`IClickerConfig`) заводится, когда конфиг пересекает границу фичи; сам тип при
этом всё равно инжектится по конкретному классу.

## Регистрация

`RootScope` зовёт `builder.RegisterConfigs()` (`VContainerBuilderExtensions.cs:76-92`):

- `ConfigTypeScanner.Scan` находит все не-абстрактные типы с `[ConfigKey]` и проверяет, что они
  реализуют `IConfig`; нарушение — `InvalidOperationException` на старте, а не молчаливый пропуск
  (`ConfigTypeScanner.cs:24-28`).
- `IConfigProvider` регистрируется Singleton'ом со списком найденных пар.
- Каждый тип конфига регистрируется Singleton-фабрикой, которая берёт инстанс из провайдера.

Ручной регистрации конфига в scope быть не должно: скан покрывает и `Foundation`, и `Features`.
В тестах сборки передаются явно — см. `AssemblyInfo.cs` и `AutoTypeScannerTests`.

## WarmUp: когда грузятся конфиги

`SceneStarter.StartAsync` прогревает конфиги параллельно с часами, до резолва `LifecycleEntity`:

```csharp
await UniTask.WhenAll(
    _configProvider.WarmUp(cancellation),
    _clock.WarmUp(cancellation));
```

`ConfigProvider.WarmUp` (`ConfigProvider.cs:25-47`):

- **идемпотентен** — `SceneStarter` зовёт его на каждой сцене, чтение происходит один раз за процесс;
- держит задачу через `.Preserve()`, потому что один `UniTask` нельзя await-ить дважды;
- **не мемоизирует неуспех**: при отмене или исключении `_warmUp` сбрасывается в `null`, и следующая
  сцена пробует заново. Без этого teardown scope посреди загрузки навсегда оставил бы игру без
  конфигов. То же правило у `IClock.WarmUp` — см. [[Time]].

Все конфиги грузятся параллельно (`UniTask.WhenAll` по всем записям), поэтому число конфигов не
превращается в число последовательных ожиданий.

Обращение к незагруженному конфигу — `InvalidOperationException` с именем типа и подсказкой про
`[ConfigKey]` (`ConfigProvider.cs:49-58`). Это ошибка сборки проекта, а не рантайм-ситуация.

## Источники и политика отказа

`ConfigResolver.Read` пробует источники по одному и берёт первый, который **разобрался**, а не
первый, где есть ключ (`ConfigResolver.cs:37-60`):

| Порядок | Источник | Откуда |
| --- | --- | --- |
| 1 | `server` | значения LiveOps, пришедшие по `ServerLoginCompletedSignal` |
| 2 | `cache` | локальная копия последних серверных значений (`IConfigStorage`) |
| 3 | `dummy` | JSON-ассет в Addressables, лежит в сборке |

Битое значение источника пишется в `LogError` (ключ, имя источника, исключение) и **уступает
следующему** (`ConfigResolver.cs:89-111`). Это та же политика, что у `SaveEnvelope` в [[SaveLoad]]:
сбой одной единицы данных не уносит остальные.

Битому `dummy` отступать некуда — он лежит рядом с кодом, и его невалидность это ошибка сборки.
Резолвер бросает, `ConfigReader` превращает исключение в `Result.HasValue = false`
(`ConfigReader.cs:59-82`), а `ConfigProvider.Load` — в `InvalidOperationException` с перечислением
источников. Исключение парсинга наружу минуя `Result` не уходит; `OperationCanceledException`
пробрасывается как есть.

Ключевая асимметрия: **порча данных снаружи не роняет игру, порча данных в сборке роняет громко.**

## Кэш серверных значений

`ConfigReader` при инициализации читает кэш через `IConfigStorage.Load()`; повреждённый JSON
отправляется в `Quarantine()` и логируется (`ConfigReader.cs:136-158`) — сравните с карантином
сейва в [[SaveLoad]].

Записывается кэш из `ConfigResolver.SetServerValues`, и только когда серверный набор **отличается**
от кэшированного (`ConfigResolver.cs:62-70`): каждый логин иначе тратил бы запись на диск впустую.

Реализация storage выбирается define-ом, партиалами `DataScopeRegistrator`: `FileConfigStorage`
(файл `Data/Config.bin` в `persistentDataPath`) или `PlayerPrefsConfigStorage` под
`PLAYER_PREFS_SAVE_ENABLED`. Тот же переключатель, что у сейвов.

## Серверные конфиги и LiveOps

`ConfigReader` подписан на `ServerLoginCompletedSignal` (`ConfigReader.cs:105`): по нему он забирает
значения из `IRemoteConfigSource` и отдаёт их резолверу. В шаблоне этот сигнал сейчас **никто не
триггерит** — реализации LiveOps нет, `EmptyRemoteConfigSource` возвращает пустой набор, и
фактически рабочим источником всегда оказывается `dummy`. См. [[LiveOps]] и [[Signals]].

Важное следствие для порядка: логин может завершиться **после** `WarmUp`. Тогда уже загруженные
конфиги останутся из dummy до следующего чтения — горячей перезагрузки уже созданных конфигов в
шаблоне нет.

## Инварианты

- Каждый тип с `[ConfigKey]` реализует `IConfig` — иначе `ConfigTypeScanner` бросает на старте
  (`ConfigTypeScannerTests`).
- У каждого ключа `[ConfigKey]` есть dummy-json в Addressables с тем же адресом. Связь
  «константа-ключ → запись в Addressables» проверяет `AddressableKeyTests`.
- Конкретный конфиг не регистрируется руками: `grep -rn "Register<.*Config" Assets/Framework
  --include=*.cs` вне тестов даёт только `IConfigProvider` в `VContainerBuilderExtensions.cs:84` и
  две реализации `IConfigStorage` в `Registrators/Data/`.
- Потребитель не проверяет `_config.IsEnabled` руками в `Init` — это делает `LifecycleGate`.
- Конфиг immutable: сеттеров у свойств нет, поля приватные.
- `WarmUp` не мемоизирует неуспешный проход.
- Исключение десериализации не покидает `ConfigReader.Read` — наружу уходит `Result`.
- Имя файла конфига в `Content/Json/` совпадает с именем типа и с ключом (см. [[Naming]]).

## Как расширять

**Новый конфиг.** Класс + `[ConfigKey]` + dummy-json. Больше ничего — ни регистрации, ни фазы.
Полный рецепт — раздел «Для агента».

**Реальный remote config** (PlayFab, GamePush): реализация `IRemoteConfigSource` в
`Integrations/<Provider>/` и триггер `ServerLoginCompletedSignal` после логина. Ни `ConfigReader`,
ни резолвер не меняются — см. [[LiveOps]].

**Required-политика и дефолтный конфиг.** Сейчас отсутствие валидного значения во всех источниках
всегда фатально. Смягчение — конфиг помечается необязательным и при
отказе получает дефолтный инстанс вместо исключения. Точка изменения — `ConfigProvider.Load`, а не
резолвер: решение «падать или нет» принимает провайдер, резолвер только сообщает результат.

**Горячая перезагрузка конфигов** после позднего логина: сейчас её нет. Добавляется вторым проходом
провайдера по `_entries` с заменой значений в словаре — но тогда потребители, держащие ссылку на
инстанс, увидят старые данные. Честный вариант — сигнал `ConfigsReloadedSignal` и явное
перечитывание у тех, кому это важно.

**Новый storage кэша** — реализация `IConfigStorage` и партиал `DataScopeRegistrator` под своим
define-ом, по образцу `FileSave` / `PlayerPrefs`.

## Тесты

`Assets/Framework/Foundation/Tests/`:

| Файл | Что закрывает |
| --- | --- |
| `ConfigTypeScannerTests` | скан по атрибуту, отказ на не-`IConfig`, пропуск абстрактных |
| `ConfigProviderTests` | идемпотентность `WarmUp`, ретрай после неуспеха, ошибка на незагруженном типе |
| `ConfigReaderTests` | карантин битого кэша, `Result` вместо исключения, реакция на логин |
| `ConfigResolverTests` | порядок источников, переход к следующему при битом значении, отказ на dummy |

Фейки: `FakeConfigReader`, `FakeConfigResolver`, `FakeConfigStorage`, `FakeRemoteConfigSource`.
Конфиги в тестах строятся из JSON через Newtonsoft (`FoundationTestConfigs.cs` /
`FeaturesTestConfigs.cs`) — тот же путь, что в проде, см. [[Testing-TDD]].

## Когда обновлять

- Изменился порядок источников или политика отказа в `ConfigResolver`.
- В `IConfig` добавлен член помимо `IsEnabled`.
- Появилась Required-политика или дефолтный конфиг.
- Изменилось поведение `WarmUp`: момент вызова, идемпотентность, реакция на отмену.
- Появился новый `IConfigStorage` или сменился формат кэша.
- Реализация LiveOps начала триггерить `ServerLoginCompletedSignal`.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Configs") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Configs") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
