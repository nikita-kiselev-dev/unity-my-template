---
title: Testing & TDD
type: architecture
area: Cross-cutting
module: Testing
status: actual
source_paths:
  - Assets/Framework/Foundation/Tests/Foundation.Tests.asmdef
  - Assets/Framework/Features/Tests/Features.Tests.asmdef
  - Assets/Framework/Foundation/Tests/Fakes/
  - Assets/Framework/Features/Tests/Fakes/
  - Assets/Framework/Features/Tests/FeaturesTestConfigs.cs
  - Assets/Framework/Foundation/Tests/AutoTypeScannerTests.cs
  - Assets/Framework/Features/Tests/RegistrationGraphTests.cs
  - Tools/fast-tests.ps1
  - Tools/fast-build.ps1
  - Tools/tdd-check.ps1
  - Tools/mutation-check.ps1
  - Tools/mutation-check.exceptions.txt
  - Tools/Mutator/
  - Tools/run-tests.ps1
  - Tools/generator-tests.ps1
  - Tools/generator-hash.ps1
  - Tools/build-generator.ps1
  - Tools/UnitTestRunner/Program.cs
  - Tools/AutoDecorators.Generator.Tests/
  - .github/workflows/generator-tests.yml
  - .github/workflows/unity-tests.yml
related:
  - "[[UI-MVVM]]"
  - "[[SaveLoad]]"
  - "[[Foundation-vs-Features]]"
  - "[[Add-UI-Window]]"
tags:
  - architecture
  - testing
  - tdd
  - nunit
updated: 2026-08-13
---

# Testing & TDD

## Для агента

TDD — обязательная методология проекта: логика новой фичи начинается с падающего теста, потом реализация, потом рефакторинг. Прочитай эту статью перед написанием любого Model/ViewModel/сервиса и перед изменением save-данных.

Быстрый цикл: `Tools/fast-tests.ps1` — компилирует сборки и гоняет тесты вне Unity за секунды, редактор закрывать не нужно. Финальная истина — Unity Test Runner (в редакторе или `Tools/run-tests.ps1` при закрытом редакторе).

**Красный прогон обязателен и проверяется машинно.** Каждый прогон `fast-tests` дописывает журнал `.agent-state/FastTests/history.jsonl`, а Stop-гейт `tdd-check` требует по нему, чтобы новый тест хотя бы раз упал **на ассерте** до того, как стал зелёным, и чтобы позеленевший тест не переписывали вместо реализации. Практический вывод: не пиши тест и реализацию в одном заходе — прогони `fast-tests` на красном тесте, иначе гейт вернёт ход. Устройство и исключения — [[Hooks]], раздел «2. tdd-check».

**Три вещи измеряют три разных свойства, и одна не заменяет другую.** `fast-tests` отвечает «тесты прошли». `tdd-check` — «процесс соблюдён: тест был красным до реализации». `mutation-check` — «поведение действительно проверено: изменение кода замечено хотя бы одним ассертом». Третье — единственная проверка, которая не опирается на артефакт, написанный тем же агентом, поэтому на нетривиальной логике её прогоняют руками (раздел «Мутационное тестирование»).

## Что тестируем и как

| Слой | Подход | Почему |
| --- | --- | --- |
| Model (домен), утилиты, стейт-машины | **TDD, test-first** | Чистый C#, ломается тихо, тест фиксирует контракт |
| ViewModel | **TDD, test-first** | Тестируем наблюдаемое поведение: команда → значение `ReadOnlyReactiveProperty` |
| Foundation-сервисы с логикой | **TDD, test-first** | `SaveEnvelope`, `Clock`, `ReactiveSignalBus` и т.п. |
| `SaveBlob` (MemoryPack) | **Тесты-контракты** (можно test-after) | Roundtrip + заполнение существующего инстанса — защита инварианта «поля только в конец» |
| View, префабы, конфиги | **Без тестов** | Клей и визуал ломаются громко; желание протестировать view = логика утекла не в тот слой |
| Core (`LifecycleEntity`) | **Без тестов** | Composition root: создать → забиндить → задиспозить. Логики быть не должно |
| Скан и граф DI | **Тесты-инварианты** (test-after) | Регистрация рефлексивна: компилятор её не проверяет, а ломается она только в рантайме |
| Source generator (`AutoDecorators`) | **Snapshot-тесты** (test-after) | Compile-time зависимость каждой фичи; регрессия выглядит как непонятная ошибка компиляции |

Критерий: тестируем поведение, которое может тихо сломаться при рефакторинге; не тестируем клей, декларации и визуал.

## Тестовые сборки

- `Assets/Framework/Foundation/Tests/` — `Foundation.Tests` (EditMode, ссылается на `Foundation`).
- `Assets/Framework/Features/Tests/` — `Features.Tests` (EditMode, ссылается на `Features`, `Foundation`, `Foundation.Tests`).
- NuGet-DLL (R3, MemoryPack.Core, ZLinq, Newtonsoft.Json, Microsoft.Bcl.TimeProvider, Microsoft.Bcl.AsyncInterfaces) перечислены в `precompiledReferences` — у тестовых asmdef `overrideReferences: true`. `Foundation.Tests` дополнительно ссылается на asmdef `UniTask` и `VContainer` (фейки UniTask-интерфейсов, `IStartable`).
- `internal`-код доступен тестам через `[assembly: InternalsVisibleTo]` в `Assets/Framework/Foundation/AssemblyInfo.cs` и `Assets/Framework/Features/AssemblyInfo.cs`.

## Стиль тестов

- NUnit, классические `Assert.*`; naming `Method_ExpectedBehavior_Condition`; классы `<ClassName>Tests`; Arrange/Act/Assert с пустыми строками.
- **Мок-фреймворков нет и не добавляем** — ручные фейки на интерфейсах проекта. Общие фейки: `Assets/Framework/Foundation/Tests/Fakes/` (`FakeLogChannel`, `FakeLogChannelFactory`, `FakeAudioController`, `FakeSceneStateMachine`, `FakeSceneService`, `FakeExternalLinkOpener`, `FakeAnalyticsService`, `FakeRemoteConfigSource`, `FakeConfigReader`, `FakeConfigResolver`, `FakeConfigStorage`, `FakeViewAnimator`, `FakeViewFactory`, `FakeViewRouter`, `FakeSaveEnvelope`, `FakeSaveStorage`, `FakeDataSaver`, `FakeAssetProvider`, `FakeTimeProvider`, `FakeRealtimeSource`, `FakeServerTimeSource`, `FakeAdsProvider`); фиче-специфичные — `Assets/Framework/Features/Tests/Fakes/` (`FakeClickerAnalytics`, `FakeSettingsCore`, `FakeInventory`). Рядом с общими фейками лежит `VersionedTestData` — `SaveBlob`-классы с общими `[SaveTag]` для проверки эволюции схемы: `VersionedTestDataV1/V2` (сейв пишется V1, читается V2 через `Migrate`), `ShrunkTestDataWide/Narrow/Guarded` (схема сузилась — блоб либо сбрасывается с `LogError`, либо штатно по `MinReadableVersion`) и `AmountTestData` (полезная нагрузка на `BigInteger`: соседний блоб, который обязан пережить чужой сбой).
- Async-логика на UniTask тестируется синхронно: раннер не ждёт async-тестов, поэтому тестовые методы — обычные `void`. Фейки завершают `UniTask` сразу (`UniTask.CompletedTask` / `UniTask.FromResult`), отложенное завершение моделируется `UniTaskCompletionSource` (пример — `FakeSaveStorage.CompleteWrite` в `SaveLoadServiceTests`); завершённый таск разворачивается через `GetAwaiter().GetResult()`.
- **Пара ctor-ов «`[Inject]` + шов»** — именованный паттерн тестового шва при DI через поля (образец — `SaveEnvelope`, `AnalyticsController`, `SaveLoadService`, `ConfigReader`, `SceneLoader`, `AdsController`, `LifecycleDecoratorPipeline`): публичный пустой конструктор **обязан** быть помечен `[Inject]`, рядом стоит `internal` конструктор с параметрами. VContainer выбирает конструктор с наибольшим числом параметров (`TypeAnalyzer`, сканирует и `NonPublic`) — без `[Inject]` он выбрал бы internal-шов и упал бы в рантайме. Тесты зовут internal-ctor напрямую через `new`, минуя контейнер. Забытый атрибут не видят ни компилятор, ни `fast-tests`, поэтому инвариант закрыт правилом `injectable-ctor-missing-attribute` в `Tools/naming-check.ps1` (см. [[Naming]]).
- Тестовые сборки (`Foundation.Tests`/`Features.Tests`) ссылаются на `Foundation` и в редакторе загружены в AppDomain, поэтому `RegisterAutoTypes` их **пропускает** (фильтр по ссылке на `nunit.framework`) — тестовые `SaveBlob`/`LifecycleEntity`/`[AutoRegistration]`-типы не должны попадать в рантайм-контейнер.
- Логика на таймерах — через инжектируемый `TimeProvider`: в тесте `FakeTimeProvider` двигает время `Advance(TimeSpan)`, таймеры срабатывают синхронно (пример — `ProgressSaverTests`).
- Конфиги строятся так же, как в проде — Newtonsoft из JSON: хелперы в `Assets/Framework/Features/Tests/FeaturesTestConfigs.cs` (фичевые) и `Assets/Framework/Foundation/Tests/FoundationTestConfigs.cs` (foundation).
- `SaveBlob`-классы в тестах: `new` + `PrepareNewData()`, состояние арранжится их публичными мутаторами.
- `LogAssert.Expect` работает только в Unity Test Runner — вне Unity такой тест будет помечен `SKIPPED`. Предпочитай `FakeLogChannel`, если тестируемый класс принимает `ILogChannel`.
- Логгер подменяется двумя способами: класс с `[AutoLogger]` — присваиванием `Logger = logger` в internal-шове (сеттер приватный, но шов лежит в том же типе; примеры — `SaveLoadService`, `SceneLoader`); класс, берущий логгер из `ILogChannelFactory` в своём `[Inject]`-методе, — через `FakeLogChannelFactory` в шове (`ConfigReader`, `SceneStateMachine`).
- Guard `if (!logger.AreLogsEnabled)` в хот-пассах проверяется так: `FakeLogChannel.SetLogsStatus(false)` → после вызова `Messages` пуст (значит вызывающий до `Log` не дошёл). `FakeLogChannel.Log` сам флаг **не** учитывает — иначе тест проверял бы фейк, а не код. Примеры — `ClickerModelTests.Click_SkipsLogging_WhenLogsDisabled`, `SceneLoaderTests.PrepareSceneLoad_LogsError_WhenLogsDisabled` (ошибки пишутся всегда).

## Скан и граф DI

Регистрация в контейнере — рефлексивный скан сборок (`AutoTypeScanner`, см. [[Initialization-LifecycleEntity]]), поэтому её ошибки компилятор не видит. Покрыто двумя уровнями:

- `AutoTypeScannerTests` (`Foundation.Tests`) — сам скан: классификация (`LifecycleEntity` / `Service` / `SaveBlob`), lifetime из атрибута, конфиги с `[ConfigKey]`, отсутствие дублей и абстрактных типов, а также оба фильтра сборок (нет ссылки на `Foundation` → пропуск; есть ссылка на `nunit.framework` → пропуск). `AutoTypeScanner.Scan` принимает набор сборок явно — в рантайме это AppDomain, в тесте фиксированный список.
- `RegistrationGraphTests` (`Features.Tests`) — статическая валидация графа: собираем те же регистрации, что `RootScope`, `BootstrapScope` и регистраторы (`internal`-перегрузки `RegisterAutoTypes`/`RegisterConfigs` со скан-результатом), и по правилам `VContainer.Internal.TypeAnalyzer` проверяем, что каждая `[Inject]`-зависимость зарегистрированного типа кем-то закрыта. Убранный у сервиса интерфейс даёт красный тест до запуска Unity.

Граф живёт в `Features.Tests`, потому что рантайм-набор регистраций — это `Foundation` и `Features` вместе.

Три осознанные поправки в тесте графа:

- Потребителями считаются только типы, которые контейнер **создаёт сам** (дефолтный `InstanceProvider`). Готовые инстансы (`RegisterInstance`), фабрики (`Func`) и конфиги (инстанс отдаёт `IConfigProvider`) VContainer возвращает как есть — их конструкторы и `[Inject]`-члены к графу отношения не имеют. Пример из жизни: в плеере R3 подставляет в `TimeProvider` свой `UnityTimeProvider` с конструктором `(FrameProvider, TimeKind)`.
- Зависимости от `UnityEngine.Object` (компоненты сцены, `ScriptableObject`-конфиги из `RootGameScope`) считаются закрытыми: их набор задаётся префабом, статически он не известен.
- Коллекция (`IReadOnlyList<T>`) требует зарегистрированного элемента — VContainer молча отдал бы пустую. Точки расширения, законно пустые в шаблоне (`IAnalyticsService` — реализации в `Integrations/`; `ILocaleSource` — платформенный источник языка в `Assembly-CSharp`), перечислены в `_optionalCollectionElements`; новый элемент там — сознательное решение.

Сам чекер тоже под тестами: `Validation_ReportsDependency_WhenNothingRegistersIt` (дыру видит), `Validation_IgnoresRegistration_WhenContainerDoesNotCreateInstance` (готовый инстанс не трогает), `Graph_AnalyzesTypesCreatedByContainer` (фильтр не выкосил всех потребителей разом).

## Тесты source generator-а

`Tools/AutoDecorators.Generator.Tests/` — обычный `dotnet test`-проект (NUnit, `Microsoft.CodeAnalysis.CSharp` 4.3, как у генератора). Прогон: `powershell -File Tools/generator-tests.ps1`.

- Тестовый код компилируется против стабов Foundation (`FrameworkStubs.cs`), потому что настоящий `Foundation.dll` собирает Unity, а генератор должен тестироваться без него. **Сигнатуры стабов обязаны совпадать** с `Assets/Framework/Foundation` — при изменении `AutoViewBinding`, `ILogChannelFactory`, `LifecycleEntity.EnableStatusLogs` и т.п. правится и стаб.
- Покрыто: snapshot генерируемого кода (`IAutoViewHost`-биндинги, `Logger` + `[Inject]`-метод, `StatusLogs`, `private` в sealed-классе, вложенный тип) и все диагностики `ADG001`–`ADG004`. В snapshot-тестах дополнительно проверяется, что сгенерированный код компилируется без ошибок.
- Переносы строк нормализуются: генератор пишет `Environment.NewLine`, а CI-раннер — Linux.
- После правок генератора нужны **оба** шага: `Tools/generator-tests.ps1` и `Tools/build-generator.ps1` (пересобрать DLL в `Assets/Framework/Analyzers/`). Тесты проверяют исходники, Unity использует собранную DLL.
- Забытый второй шаг — тихий провал: тесты зелёные, а редактор компилирует старым генератором. Ловится хэшем `Assets/Framework/Analyzers/AutoDecorators.Generator.dll.hash`, который пишет сам `build-generator.ps1`; сверяет Stop-хук `Tools/hook-generator-hash.ps1` (ручной прогон — `powershell -File Tools/generator-hash.ps1 -Check`).

## CI

- `.github/workflows/generator-tests.yml` — GitHub-hosted раннер, `dotnet test` тестов генератора на каждый push в `main` и на PR. Единственные тесты, которым не нужен Unity.
- `.github/workflows/unity-tests.yml` — EditMode-тесты через `Tools/run-tests.ps1`. Требует self-hosted раннера с Unity того же билда и активированной лицензией; такого раннера у проекта нет, поэтому workflow запускается только вручную (`workflow_dispatch`).

## Признаки тестируемого дизайна

- Время и рандом инжектятся (`IClock`, `TimeProvider`), не берутся из `DateTime.Now` / `UnityEngine.Random` внутри логики. `TimeProvider` (интервалы, таймеры) зарегистрирован в `RootScope`, эталон — `ProgressSaver` + `FakeTimeProvider`; текущее время — `IClock`, см. [[Time]].
- Логика фичи создаётся конструктором в одну строку — без сцены, префабов и VContainer. Если тесту нужен DI-контейнер, дизайн протёк.
- Model принимает интерфейсы на границах (`IInventory`, `ISignalBus`); конкретные `SaveBlob`-классы (`*Data`) инстанцируются напрямую — это нормально.

## Контракт save-данных

Для каждого конкретного `SaveBlob`-класса обязателен roundtrip-тест (пример — `SaveBlobContractTests`): сериализация → десериализация **в существующий инстанс** (`ref`-перегрузка, как в `SaveEnvelope.Deserialize`) → проверка значений и `Assert.AreSame`. Это автоматическая защита инвариантов MemoryPack: новые члены только в конец, без удаления и перестановки.

`ItemsData` (и любые `SaveBlob` с `BigInteger` и т.п.) требуют регистрации форматтеров: `SaveLoadBootstrap.Init()` в `[OneTimeSetUp]` — в EditMode `RuntimeInitializeOnLoadMethod` не вызывается.

## Как запускать

| Способ | Когда | Команда |
| --- | --- | --- |
| Быстрый цикл (агент) | Каждая итерация red-green-refactor | `powershell -File Tools/fast-tests.ps1` |
| Мутации по дифу | После зелёного цикла на нетривиальной логике | `powershell -File Tools/mutation-check.ps1` |
| Тесты генератора | После правок `Tools/AutoDecorators.Generator/` | `powershell -File Tools/generator-tests.ps1` |
| Unity Test Runner (редактор) | Финальная проверка пользователем | Window → General → Test Runner → Run All (EditMode) |
| Unity CLI | Финальная проверка при закрытом редакторе | `powershell -File Tools/run-tests.ps1` |

`fast-tests.ps1` компилирует `Foundation`/`Features`/`Foundation.Tests`/`Features.Tests` Roslyn-ом из поставки Unity (дефайны — из сгенерированных csproj, генераторы MemoryPack/AutoDecorators подключены) и запускает `Tools/UnitTestRunner`. Ограничения: требует сгенерированных csproj (Unity → Assets → Open C# Project); не гоняет PlayMode и `LogAssert`-тесты; Unity Test Runner остаётся источником истины.

Сама компиляция вынесена в `Tools/fast-build.ps1` — его дот-сорсят и `fast-tests.ps1`, и `mutation-check.ps1`. Копий быть не должно: если мутант компилируется не тем же набором ссылок и дефайнов, что тесты, измерение перестаёт что-либо значить. `fast-build.ps1` умеет собрать подмножество сборок (`-Only`) и подменить исходник на мутированную копию (`-SourceOverrides`) — на этом держится прогон мутанта за секунды вместо полной пересборки.

### Референсы тестовых сборок

Обе тестовые сборки стоят с `overrideReferences: true`, поэтому недостающая ссылка в asmdef — ошибка компиляции в Unity. Раньше `fast-tests` компилировал их **суперсетом** (набор одного asmdef + явно добавленные UniTask/VContainer/Addressables/Newtonsoft/Bcl-DLL, общий на обе сборки) — единственный класс ошибок, который агент не мог увидеть сам: зелёный скрипт, красный Test Runner у пользователя. Теперь список собирается строго по asmdef:

- `references` — по имени сборки: сначала свежесобранная DLL в `Temp/FastTests`, затем `Library/ScriptAssemblies`. Поддержана и `GUID:`-форма записи (разворачивается по `.asmdef.meta`).
- `precompiledReferences` — по имени файла, из пула `HintPath`-ов всех сгенерированных csproj; если там нет — поиск в `Assets/Packages`, `Assets/Plugins`, `Library/PackageCache`.
- Из csproj берутся **только** сборки из каталога установки Unity: движок и рантаймовые фасады, которых в asmdef нет и не может быть (`noEngineReferences: false`).

Объявленная, но не найденная сборка — явная ошибка с именем и перечислением мест, где искали, а не тихий недобор ссылок. `run-tests.ps1` синхронизировать не нужно: он гоняет тесты самим Unity. `UnitTestRunner` резолвит сборки в рантайме по `--probe`-каталогам и намеренно остаётся широким — это загрузка, а не компиляция.

## Property-based инварианты

`Assets/Framework/Foundation/Tests/PropertyCheck.cs` — минимальный property-based раннер (своя
реализация: список тестовых зависимостей проекта закрыт, а нужным инвариантам хватает нескольких
десятков строк). `ForAll(generate, assert, cases, seed, shrink, describe)` прогоняет инвариант на
сгенерированных входах; генераторы `BigIntegerValue`, `Duration`, `Sequence` и шаг уменьшения
`DropLast` лежат там же.

- **Seed фиксирован** (`PropertyCheck.DefaultSeed`) и печатается в сообщении падения вместе с
  номером кейса: флаки-гейт начинают игнорировать, а контрпример обязан воспроизводиться руками.
- **Уменьшение контрпримера** задаёт вызывающий (`shrink`), потому что «меньше» определяется
  доменом, а не структурой типа. Без него сообщение вида «упало на последовательности из 24
  операций» бесполезно. `shrink` **нельзя** задавать, если assert трогает состояние, общее для
  всех кейсов (например одни часы на тест): повторный прогон уменьшенного входа сдвинет это
  состояние ещё раз, и «уменьшенный» контрпример будет врать.
- **Бюджет времени** (`budgetMs`, по умолчанию 1000 мс): при исчерпании прогон прекращается с
  сообщением в вывод, а не падает — падение по времени сделало бы гейт флаки на медленной машине,
  а молчаливое усечение читалось бы как «проверено всё». Бюджет заодно диагностический: если тест
  успевает единицы кейсов из сотни, дорог не раннер, а сам кейс.
- **Соизмеряй генератор с ценой кейса.** Первая версия `ClockPropertyTests` вешала Unity Test
  Runner на 4+ минуты: `Duration` генерировал интервалы до 3 дней, а `Clock` тикает раз в секунду
  через `Observable.Interval`, то есть один `Advance` прокручивал до 259 200 срабатываний таймера.
  Границы генераторов (`Duration(maxSeconds)`, величина `BigIntegerValue`, длина `Sequence`)
  выбираются по тому, что делает с данными потребитель, а не по принципу «шире значит лучше».
- **Когда property, а когда пример.** Property — для свойства, которое обязано держаться на
  *любом* входе: «счётчик не уходит в минус», «время не идёт назад», «roundtrip сохраняет
  значение». Пример — для конкретного сценария с осмысленными числами: он читается как
  документация поведения, а property на его месте только запутает. Одно не заменяет другое:
  `ItemsDataTests` и `ItemsDataPropertyTests` живут рядом.
- Покрыто: `ResultPropertyTests` (`Map` / `Match` / `TryGet` не меняют исход), `ClockPropertyTests`
  (монотонность `ServerUtcNow`, равенство anchor + накопленный ход, `Countdown` завершается ровно
  на `TimeSpan.Zero`), `ItemsDataPropertyTests` (счётчик не отрицателен, отклонённая операция не
  меняет состояние, неположительная сумма всегда отклоняется, сумма начислений точна),
  `SaveBlobPropertyTests` (roundtrip `ItemsData` на генерируемых `BigInteger`).

Property-тест на **уже написанный** корректный код красным не бывает — красного прогона без порчи
рабочего кода не существует. Это та же категория, что `SaveBlob` roundtrip и инварианты скана DI:
test-after по таблице выше, и гейт `tdd-check` пропускает её через строку в
`Tools/tdd-check.exceptions.txt` на **класс** целиком. Порча рабочего кода ради красного прогона —
не соблюдение процесса, а его имитация.

## Журнал прогонов

`UnitTestRunner` по ключу `--journal <path>` дописывает строку на каждый тест:
`{"utc","test","outcome","errorType"}`, где `outcome` — `passed` / `failed` / `skipped`, а
`errorType` — имя типа исключения. `fast-tests.ps1` всегда передаёт
`--journal .agent-state/FastTests/history.jsonl`; журнал урезается до последних 20 000 строк.

Зачем нужен `errorType`: красный по `NullReferenceException` или `MissingMethodException` — это не
фаза red, а отсутствующий код. Тест упал, не дойдя до проверки поведения, и такой прогон ничего не
доказывает. Гейт `tdd-check` различает эти два случая и требует красного **по ассерту**.

Если фикстура не поднялась, красными в журнал пишутся все её тесты: иначе у нового теста была бы
пустая история вместо падения, и гейт увидел бы «тест ни разу не прогонялся».

Сбой записи журнала не красит тесты — он печатается строкой в вывод. Журнал это инструмент гейта, а
не результат прогона.

## Мутационное тестирование

`powershell -File Tools/mutation-check.ps1` вносит в изменённый код одну точечную правку поведения и
смотрит, покраснеет ли набор тестов. Покраснел — **мутант убит**. Остался зелёным — **выживший
мутант**: строка исполняется, но её поведение никто не проверяет.

Зачем это отдельно от покрытия: coverage отвечает на вопрос «строка исполнилась», мутации — «изменение
поведения замечено». Для агента разница принципиальная, потому что тесты первого рода (исполняют код,
но не проверяют следствий) он пишет уверенно и они проходят весь остальной Stop-контур молча.

**Что мутируется.** Только рантайм-код `Foundation` и `Features` (тесты и `Editor/` исключены) и только
члены, которых коснулся диф: полный прогон по сборке не влезает ни в какой таймаут. Изменённые строки
из `git diff -U0` разворачиваются до объемлющего метода/свойства/поля, а `-All` снимает ограничение по
строкам для перечисленных файлов.

**Отсев кода, который по конвенции не тестируется.** Выживший мутант обязан означать дыру в ассертах.
В коде, который проект тестировать и не собирался (View, composition root, адаптеры к внешним
системам), он не означает ничего — и вытесняет настоящие находки ровно в той пропорции, в какой
такого кода больше. Три правила, единица отсева — файл:

| Правило | Как определяется | Примеры |
| --- | --- | --- |
| Наследник `UnityEngine.Object` | семантика: `Mutator scan` строит `CSharpCompilation` по тому же `.rsp`, которым собирается мутант, и обходит цепочку базовых типов | `MonoBehaviour`, `ScriptableObject`, `GradientColor : BaseMeshEffect`, все `*View` и `*Scope` |
| Суффикс `Core` | имя типа (composition root по [[Naming]]) | `DailyBonusCore`, `SettingsCore` |
| Роль «тонкий адаптер к внешней системе» | строка в `Tools/mutation-check.exceptions.txt` с **обязательной** причиной | `AddressableAssetProvider`, `FileSaveStorage`, `PlayerPrefsSaveStorage`, `CanvasProvider` |

Первые два выводятся машинно, третье — нет: «тонкий адаптер» это роль, а не свойство типа, и
эвристика по имени или пути промахнулась бы в обе стороны. Явный список честнее: «этот класс не
тестируется» — решение автора, и оно записано с причиной.

Файл исключается, только если исключён **каждый** объявленный в нём тип: единица мутации — файл, и
половинчатое решение спрятало бы живую логику, лежащую рядом с `MonoBehaviour`. Сколько файлов ушло
и по какому правилу, печатается строкой отчёта — молчаливый отсев читался бы как «здесь всё чисто».
Скан отделён от планирования: `plan` и `apply` остаются чисто синтаксическими и детерминированными,
иначе мутант перестал бы восстанавливаться по индексу.

Если `UnityEngine.Object` не разрешается по ссылкам, скан падает, а не отдаёт пустой список:
выключившийся фильтр неотличим от «непроверяемого кода в изменениях нет».

**Операторы** (по восемь пар, обе стороны каждой): `> ↔ >=`, `< ↔ <=`, `== ↔ !=`, `&& ↔ ||`,
`true ↔ false`, `+ ↔ -`, удаление вызова-инструкции, `return x` → `return default`. Мутатор —
`Tools/Mutator/` (Roslyn из поставки Unity, .NET SDK не нужен): планирует мутации по syntax tree и
выдаёт мутированную копию файла, а прогоном управляет `mutation-check.ps1`.

**Исходы прогона.** Убит, выжил, не скомпилировался (`BROKEN`), таймаут. `BROKEN` — честная категория,
а не убитый мутант: `+ ↔ -` на `DateTime - DateTime` невыразим на уровне синтаксиса, и записать такое
в «убитые» значило бы завысить силу тестов. Таймаут считается убийством: мутация условия цикла умеет
сделать тест вечным, и это тоже замеченное изменение поведения.

**Что сознательно не мутируется**, чтобы шум не вытеснил находки: значения атрибутов (это метаданные
компиляции — порядок фаз, ключи Addressables, а не проверяемое поведение); `+` рядом со строковым
литералом (конкатенация не компилируется через `-`); удаление `Log*`-вызовов (лог — побочный канал,
тестами он не наблюдается, см. [[Logger]], поэтому такой выживший эквивалентен по построению);
`return false` / `return 0` / `return null` → `return default` (значение и так равно `default` своего
типа — мутант выживает всегда и на любом наборе тестов). Критерий один: мутация, которая **не может**
изменить поведение, не мутация, а строка в отчёте, вытесняющая настоящую находку.

**Две выживаемости, а не одна.** Операторы делятся на **поведенческие** (`relational-boundary`,
`equality`, `logical`, `arithmetic` — меняют результат вычисления) и **слабые** (`statement-removal`,
`boolean-literal`, `return-default` — сносят действие, наблюдаемое только через Unity или соседний
объект). У вторых выживаемость систематически выше по построению, и сложенные в одну цифру они дают
метрику, которая ничего не измеряет: общие 64% состояли из 45% по
поведенческим и 69% по слабым. Поэтому верхняя строка отчёта — выживаемость по поведенческим, слабые
идут вторым блоком с пометкой; выжившие печатаются двумя списками в том же порядке. Слабых из плана
не выбрасываем: удалённый `SetParent` бывает и настоящей находкой, просто разбирать его нужно после.

**Машиночитаемый результат.** `-Json <path>` дописывает по строке на мутанта
(`file`, `line`, `column`, `operator`, `original`, `mutated`, `preview`, `outcome` ∈
`killed` / `survived` / `broken` / `timeout`). Строка пишется сразу после исхода, поэтому прогон
батчами переживает обрыв, а сводка считается по всем батчам сразу — парсить человеческий вывод не нужно.
Файл пишется UTF-8 **без BOM** через `File.AppendAllText`: `Add-Content -Encoding UTF8` в PS 5.1
ставит BOM при создании, и строгий JSONL-парсер падает на первой же записи.

**Как читать выжившего.** Он либо дыра в ассертах, либо **эквивалентная мутация** — поведение реально
не изменилось (защитная ветка, идемпотентный вызов, недостижимая граница). Второе проверяется глазами
и фиксируется в тикете; считать эквивалентным по умолчанию нельзя, иначе инструмент превращается в
формальность.

Оба вида на живом примере — прогон по `Clock.cs`. **Настоящие находки:** удаление
`SetAnchor(DateTime.UtcNow, ClockTrust.LocalFallback)` в конструкторе и в `Synchronize` выживало
(инвариант «часы идут с первой секунды процесса» не был закреплён ассертом), как и удаление
`_serverNow.Dispose()`. **Эквивалентная мутация:** в `Remaining` замена `remaining > TimeSpan.Zero`
на `>=` меняет только ветку при `remaining == 0`, а обе ветки там дают `TimeSpan.Zero` — теста,
который бы это заметил, не существует в принципе.

**Выживший мутант — готовая красная фаза.** Тест, закрывающий находку, пишется на **уже работающий**
код и потому зелёный с первого прогона, а гейт `tdd-check` требует красного. Правильный порядок:
внести мутацию из отчёта в код, прогнать `fast-tests` (новый тест падает на ассерте — запись в
журнале), вернуть код, прогнать снова (зелено). Красная фаза здесь настоящая и точная: тест падает
ровно на том поведении, из-за которого он написан, а не на случайно испорченном коде. Так были
закрыты три находки в `Clock.cs`.

**Потолок и честность отчёта.** `-Limit` (по умолчанию 40) ограничивает число мутантов за прогон, и
число непроверенных печатается отдельной строкой: молчаливое усечение читалось бы как «покрыто всё».

**Самопроверка.** `mutation-check.ps1 -SelfTest` гоняет мутатор по синтетическому исходнику и
проверяет три вещи: каждый оператор сработал, ни один мутант не сломал компиляцию, отсев по
конвенции исключил наследника Unity-типа и `*Core` и не тронул остальное. Без неё поломка
измерительного прибора выглядит как «мутантов нет» или «все убиты» — молча и правдоподобно.
Unity-типы в самопроверке объявлены прямо в синтетическом исходнике, поэтому цепочка наследования
проверяется без движковых DLL.

**В Stop-цепочку не включён** — обоснование в [[Hooks]].

## Порядок работы над фичей (TDD)

1. Тикет → скоуп → какие Model/VM появятся.
2. Красный тест на первое поведение Model (`Features.Tests`) — **прогнать `fast-tests` на нём**,
   пока реализации нет: без красной записи в журнале гейт `tdd-check` вернёт ход.
3. Минимальная реализация до зелёного (`Tools/fast-tests.ps1`).
4. Рефакторинг при зелёных тестах; повторять 2–4. На нетривиальной логике (границы, ветвления,
   накопление состояния) — `Tools/mutation-check.ps1`: зелёный прогон ещё не значит, что ассерты
   что-то держат.
5. VM: тесты на команды/состояние с фейками → реализация.
6. Data: roundtrip-тест контракта.
7. Core, view, префаб — без тестов; ручные шаги пользователю.
8. Финальный прогон в Unity Test Runner + подтверждение компиляции пользователем — только после этого тикет в `Done`.

## Инварианты

- Любой `Subscribe()` в тестах — `using`/`Dispose` или короткоживущий; тесты не оставляют живых подписок между кейсами.
- Тестовые сборки — только `includePlatforms: Editor` + `defineConstraints: UNITY_INCLUDE_TESTS`; в билд не попадают.
- Фейки не содержат логики — только запись вызовов и настраиваемые возвраты.
- Тест, требующий сцену/GameObject/Addressables, — сигнал пересмотреть дизайн, а не писать PlayMode-тест.

## Когда обновлять

- Добавили общий фейк или тест-хелпер — допиши в секцию «Стиль тестов».
- Изменили процесс запуска (`fast-tests.ps1`, `fast-build.ps1`, `run-tests.ps1`, `generator-tests.ps1`, `UnitTestRunner`) — обнови «Как запускать».
- Добавили или убрали оператор мутации, изменили правила отсева шума — обнови «Мутационное тестирование» и список ожидаемых операторов в `-SelfTest`.
- Решили, что очередной тип не тестируется, — строка в `Tools/mutation-check.exceptions.txt` с причиной, а не правка эвристики в `Mutator scan`.
- Изменили правила регистрации или добавили ручную регистрацию в scope — синхронизируй `RegistrationGraphTests` и секцию «Скан и граф DI».
- Изменили публичные типы, на которые опирается генератор — обнови `FrameworkStubs.cs`.
- Появился self-hosted раннер с Unity — включи `unity-tests.yml` по push/PR и обнови секцию «CI».
- Появились PlayMode-тесты или мок-фреймворк (решение пересмотрено) — перепиши соответствующие секции и AGENTS.md.

## Last Verified

2026-08-13: 372 теста зелёные через `Tools/fast-tests.ps1` (1 пропущен — `UIEffectObjectPoolTests.Release_DoubleRelease_DoesNotDuplicateElement`, только Unity Test Runner); журнал прогонов и гейт `tdd-check` проверены на искусственном журнале и на живой поломке реализации. 2026-08-13: `mutation-check.ps1 -SelfTest` — 21 мутант, все 14 направлений операторов сработали, ни один не сломал компиляцию, отсев по конвенции зелёный (4 файла скана); прогон по `Clock.cs` — 19 мутантов, 17 убито, 1 выжил (эквивалентный), 1 `BROKEN`; прогон по реальному дифу (31 файл, потолок 40 мутантов) — 138 с. 2026-08-13: отсев проверен на данных переписи мутаций — 48 файлов вне мутации, выживших 842 → 490, выживаемость по поведенческим 45% → 30%. 2026-07-27: 12 тестов генератора зелёные через `Tools/generator-tests.ps1`.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Testing-TDD") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Testing-TDD") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```