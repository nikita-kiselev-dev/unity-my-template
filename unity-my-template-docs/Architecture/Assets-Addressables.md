---
title: Assets & Addressables
type: architecture
area: Foundation
module: Asset
status: actual
source_paths:
  - Assets/Framework/Foundation/Asset/IAssetProvider.cs
  - Assets/Framework/Foundation/Asset/AddressableAssetProvider.cs
  - Assets/Framework/Foundation/Asset/IAssetScope.cs
  - Assets/Framework/Foundation/Asset/IAssetScopeFactory.cs
  - Assets/Framework/Foundation/Asset/IAssetOwnerHost.cs
  - Assets/Framework/Foundation/Asset/AssetScope.cs
  - Assets/Framework/Foundation/Asset/AssetOwnership.cs
  - Assets/Framework/Foundation/Asset/CachedAssetHandle.cs
  - Assets/Framework/Foundation/Asset/AssetInstantiation.cs
  - Assets/Framework/Foundation/Asset/InstanceRegistry.cs
  - Assets/Framework/Foundation/Asset/InflightLoads.cs
  - Assets/Framework/Foundation/Asset/IAssetProviderDiagnostics.cs
  - Assets/Framework/Foundation/Asset/Icons/IconProvider.cs
  - Assets/Framework/Foundation/UI/Views/ViewFactory.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Decorators/AutoView/AutoViewEntity.cs
  - Assets/Framework/Features/DailyBonus/Scripts/DailyBonusCore.cs
  - Assets/Framework/Features/Tests/AddressableKeyTests.cs
related:
  - "[[UI-Views]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Foundation-vs-Features]]"
  - "[[Testing-TDD]]"
  - "[[Naming]]"
tags:
  - architecture
  - foundation
  - asset
  - addressables
  - lifetime
updated: 2026-08-26
---

# Assets & Addressables

## Для агента

Открывай эту статью, если фиче нужно загрузить префаб, спрайт, аудио или `TextAsset`, либо если непонятно, кто и когда освобождает уже загруженный ассет.

Главный вопрос при любой загрузке — **кто освободит**. Ответ зависит от слоя, и слой определяет интерфейс:

| Слой | Что инжектить | Как грузить | Кто освобождает |
| --- | --- | --- | --- |
| `Features` | `IAssetScopeFactory` | `CreateScope()` → грузить через scope | `scope.Dispose()` в `Dispose` владельца |
| `Foundation`, ассет живёт весь процесс | `IAssetProvider` | `LoadAssetAsync(key, persistent: true)` | никто до явного `ReleaseCompletely` |
| `Foundation`, ассет живёт до конца сцены | `IAssetProvider` | `LoadAssetAsync(key)` (дефолт) | шторка загрузки — `ReleaseAllNonPersistent` |

В фичевом коде выбора нет: `IAssetProvider` там не инжектится, а `IAssetScope` не даёт ни `persistent`, ни `ReleaseAsset` — забыть релиз или отключить его флагом нечем. Два нижних варианта — для инфраструктуры `Foundation`, которая по смыслу переживает своего вызывающего (`IconProvider`, `AudioClipLoader`, `CanvasProvider`, `ConfigResolver`).

Если сущность объявляет view через `[AutoWindow]` / `[AutoPopup]`, ассетами занимается декоратор — руками грузить и релизить не нужно (см. [[UI-Views]]).

## Назначение

`AddressableAssetProvider` — единственная точка доступа к Addressables в проекте. Прямых вызовов `Addressables.LoadAssetAsync` в фичах нет: провайдер добавляет поверх Addressables кэш по ключу, дедупликацию параллельных загрузок, учёт созданных инстансов и групповое освобождение по смене сцены. Наружу он смотрит тремя интерфейсами разной ширины — `IAssetScopeFactory` (для фич), `IAssetScope` (владение подмножеством ключей), `IAssetProvider` (полная поверхность, для `Foundation`).

Реализация одна — `AddressableAssetProvider`, `Lifetime.Singleton`. Singleton, а не Scoped: при Scoped у Singleton-потребителей (`IIconProvider` → `IAssetProvider`) появился бы отдельный root-инстанс рядом со сценовым — два кэша под одним интерфейсом (captive dependency, инвариант закрыт `RegistrationGraphTests`). Изоляцию ассетов по сценам обеспечивает не lifetime, а освобождение по шторке.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `IAssetProvider` / `AddressableAssetProvider` | загрузка, кэш, инстансы, освобождение; Singleton |
| `IAssetScopeFactory` | узкая грань провайдера: только `CreateScope()`; то, что инжектят фичи |
| `IAssetScope` / `AssetScope` | владение подмножеством ключей с групповым релизом в `Dispose` |
| `IAssetOwnerHost` | `internal`-грань провайдера с явным владельцем; её видит только `AssetScope` |
| `AssetOwnership` | `internal`-учёт «ключ → владельцы» и «ключ → заявки на persistent» |
| `CachedAssetHandle` | immutable-запись кэша: `AsyncOperationHandle` + сам `Object` + запрошенный тип; заполняется конструктором в `AddressableAssetProvider` |
| `AssetInstantiation` | `internal`-хелпер: инстанс с отложенным `Awake`, текст ошибки об отсутствующем компоненте |
| `InstanceRegistry<TInstance>` | `internal`-учёт «ключ ↔ инстансы»; живость — делегатом, идентичность — по ссылке |
| `IAssetProviderDiagnostics` / `AssetProviderSnapshot` | read-only снапшот состояния для дебаг-оверлея |
| `IIconProvider` / `IconProvider` | кэш спрайтов и атласов поверх провайдера; Singleton |

## Контракт `IAssetProvider`

| Член | Что делает |
| --- | --- |
| `LoadAssetAsync<T>(key \| AssetReference, persistent, ct)` | грузит и кэширует ассет по ключу; повторный вызов отдаёт кэш |
| `InstantiateAsync<T>(key \| AssetReference, parent, worldPositionStays, setActive, persistent, ct)` | грузит префаб и создаёт инстанс, возвращая компонент `T` |
| `ReleaseInstance(GameObject)` | уничтожает инстанс, **ассет оставляет в кэше** |
| `ReleaseAsset(key \| AssetReference)` | корневой владелец отпускает ключ; handle освобождается, если ключ не persistent, его не держит другой владелец и нет живых инстансов |
| `ReleaseCompletely(key)` | уничтожает инстансы **корневого владельца**, снимает его заявку на persistent и отпускает ключ |
| `CreateScope()` (из `IAssetScopeFactory`) | новый `IAssetScope` поверх этого провайдера |

## Контракт `IAssetScope`

| Член | Что делает |
| --- | --- |
| `LoadAssetAsync<T>(key \| AssetReference, ct)` | как у провайдера, плюс ключ попадает в трекинг scope |
| `InstantiateAsync<T>(key \| AssetReference, parent, worldPositionStays, setActive, ct)` | то же для инстанса |
| `ReleaseInstance(GameObject)` | уничтожает инстанс, ассет оставляет за scope |
| `ReleaseCompletely(key)` | отпускает ключ досрочно от имени этого владельца и убирает его из трекинга |
| `CreateScope()` | вложенный scope того же провайдера |
| `Dispose()` | `ReleaseCompletely` по всем своим ключам |

`persistent` и `ReleaseAsset` в scope **отсутствуют осознанно**: первый вывел бы ключ из-под релиза владельца, второй — тихо не сработал бы при живых инстансах. Оба доступны через `IAssetProvider`, который инжектит только `Foundation`.

Перегрузки с `AssetReference` делегируют в строковые: `reference.RuntimeKey.ToString()` — валидный Addressables-ключ (GUID), поэтому кэш, persistent-флаги и учёт инстансов у них общие с ключами-строками.

## Время жизни

**Дефолт — до шторки.** `AddressableAssetProvider` подписан на `LoadingCurtainShownSignal` и по нему вызывает `ReleaseCompletely` для каждого не-persistent ключа. Шторка поднимается перед загрузкой новой сцены, поэтому «ассет живёт до конца сцены» — это не lifetime контейнера, а именно эта подписка. `IconProvider` подписан на тот же сигнал и чистит свои словари: иначе в них остались бы записи с fake-null, которые молча перезагружались бы поштучно.

**`persistent: true`** — ассет переживает шторку. Это не флаг ключа, а **заявка владельца**: она ставится после успешного резолва, отдельно для каждого вызывающего (одну in-flight загрузку могут ждать несколько вызовов с разным флагом), и снимается только вместе с владением — то есть тем, кто её поставил. Ключ считается persistent, пока заявку держит хотя бы один владелец.

**`IAssetScope`** — владение подмножеством ключей у фичи, которая живёт меньше сцены.

```csharp
_assets = _assetScopeFactory.CreateScope();
var dayFactory = new DailyBonusDayViewSpawner(_assets);
await dayFactory.CreateDayViews(days);

// ...

public override void Dispose()
{
    _assets?.Dispose();
    _assets = null;
    base.Dispose();
}
```

Scope — тонкая обёртка: он трекает ключи, загруженные **через себя**, и в `Dispose` отпускает каждый от своего имени. Ассет, который должен пережить фичу, всё равно грузится корневым провайдером, а не scope-ом: scope не умеет ставить заявку на persistent.

`ReleaseInstance` делегируется в провайдер как есть (ассет остаётся за scope), `ReleaseCompletely` дополнительно убирает ключ из трекинга.

Вложенные scope-ы — **независимые соседи** поверх одного провайдера, а не иерархия: `scope.CreateScope()` возвращает scope того же провайдера, и dispose внешнего не трогает ключи вложенного.

## Владение ключом

Один ключ может держать несколько владельцев: две фичи грузят один префаб, фича грузит спрайт, который уже лежит в `IconProvider`. Поэтому релиз адресован не ключу, а паре «ключ + владелец»: `ReleaseCompletely` снимает владение того, кто его позвал, и освобождает handle, только когда ключ не держит **никто** и не осталось живых инстансов.

Владельцев ровно два вида:

- **корневой** — все прямые вызовы `IAssetProvider`. Инфраструктура `Foundation` scope-ов не заводит, но её ключи тоже кто-то должен держать;
- **`AssetScope`** — по владельцу на каждый `CreateScope()`. Scope передаёт себя провайдеру и потому распоряжается только своим.

Счётчик живёт **в провайдере**, а не в scope: вопрос «держит ли этот ключ кто-то ещё» может задать только тот, кто видит всех владельцев. Собственный счётчик у scope означал бы, что scope знает о соседях, — ровно тот цикл знания, который запрещает [[Class-Interaction]]. Владелец при этом не протекает в публичную поверхность: `IAssetProvider` остаётся без параметра `owner`, а явную грань `IAssetOwnerHost` видит только `AssetScope`.

Владение распространяется на три вещи сразу:

| Что | Правило |
| --- | --- |
| handle ассета | освобождается, когда владельцев не осталось и нет живых инстансов |
| инстансы | `ReleaseCompletely` уничтожает только созданные этим владельцем |
| заявка на persistent | снимается только тем, кто её поставил |

Отсюда требование к `ViewFactory`: `CreateView` принимает `IAssetScope owner` и создаёт инстанс через него. Иначе ключ префаба принадлежал бы scope-у сущности, а сам GameObject — корневому владельцу, и `Dispose` сущности освободил бы ключ, оставив окно висеть до выгрузки сцены.

Шторка в этой картине — не отдельный механизм, а **корневой владелец, отпускающий всё, что не заявлено persistent**. Ключ, который держит живой scope, её переживает: у него есть владелец, и он сам его освободит в своём `Dispose`.

## Кэш и параллельные загрузки

- `_cachedHandles` — ключ → `CachedAssetHandle`. Повторный `LoadAssetAsync` того же ключа не идёт в Addressables.
- **Попадание в кэш синхронно**: `LoadAssetAsync` отдаёт `UniTask.FromResult` до входа в async-машину. Это горячий путь — спавн иконок, каждое открытие окна через `ViewFactory`. В async-ветку уходят только промах кэша, несовпадение типа и отменённый токен: так исключение остаётся в задаче, а не летит синхронно из метода.
- Тип запоминается: запрос уже закэшированного (или загружаемого) ключа с другим `T` — `InvalidOperationException`, а не тихий `null`.
- `_inflightLoads` (`InflightLoads<CachedAssetHandle>`) — дедупликация: параллельные запросы одного ключа ждут результат одной загрузки, а не запускают N. Первый вызывающий делает `Begin` и грузит, остальные получают обещание через `Join`; по завершении инициатор раздаёт результат (`Complete`) или исключение (`Fail`) всем сразу.
- **Обещание — `UniTaskCompletionSource<T>`, и это не деталь вкуса.** `.Preserve()` здесь не работает: `MemoizeSource` запоминает результат, но пока задача не завершилась, любой `OnCompleted` уходит во внутренний источник, а тот допускает ровно одного ожидающего — второй получает `InvalidOperationException: Already continuation registered`. То есть `Preserve` даёт повторный `await` **после** завершения, а join-путь нужен для нескольких ожидающих **одновременно**. `AsyncLazy<T>` не подходит по той же причине — у него один `completionSource` на всех. У `UniTaskCompletionSource<T>` для этого есть `secondaryContinuationList`. Механизм был сломан с момента появления и не проявлялся, пока шаблон грузил ассеты строго последовательно.
- Тип сверяется **до** ожидания: присоединиться к чужой загрузке с другим `T` нельзя, `Join` бросает сразу.
- Провал или отмена загрузки освобождают handle и **не мемоизируются**: запись уходит из `_inflightLoads` в `Complete` / `Fail`, следующая попытка грузит заново. Синхронный бросок Addressables тоже проходит через `Fail` — иначе присоединившиеся ждали бы обещание, которое никто не закроет.
- `cancellationToken` навешивается через `AttachExternalCancellation`, но кэш и in-flight отдаются синхронно — поэтому отмена проверяется явно (`ThrowIfCancellationRequested` в `ResolveHandleAsync` и после `await` в `InstantiateAsync`). Без этого отменённый вызывающий получал бы ассет, а `InstantiateAsync` создавал бы инстанс-сироту.
- **Брошенная загрузка не кэшируется.** Оборвать саму загрузку нельзя — её ждут вызывающие без токена (`IconProvider.OnAtlasRequested`), для них отмена обернулась бы `LogError` и отсутствующим атласом. Поэтому провайдер считает не отмены, а **ждущих**: каждый вызывающий регистрируется до `await` и снимается в `finally`. Если к моменту завершения загрузки ждущих не осталось (шторка сменила сцену, все токены отменены), владельца у ключа не появится — handle освобождается сразу. Ждущие есть — результат кэшируется как обычно. Ждущий регистрируется **до** старта задачи: закончись загрузка синхронно, инициатор получил бы уже освобождённый handle.
- Загрузка, завершившаяся после `Dispose` провайдера, освобождает свой handle и не кэшируется: в `_cachedHandles` её результат уже никто не заберёт.

## Инстансы

`InstantiateAsync` гасит закэшированный префаб на время `Instantiate` и возвращает исходный `activeSelf` в `finally`: иначе `Awake` / `OnEnable` на инстансе сработали бы до того, как вызывающий его настроил. Флаг `setActive` включает уже созданный инстанс.

Если на префабе нет компонента `T`, инстанс уничтожается и летит `InvalidOperationException` с ключом и именем типа — префаб в кэше остаётся.

Учёт инстансов вынесен в `InstanceRegistry<GameObject>` — двусторонняя карта `ключ ↔ инстансы`, чтобы `ReleaseAsset` не выдернул ассет из-под живых объектов. Живость реестр узнаёт делегатом (`instance => instance != null`), а инстансы различает **по ссылке**, а не через `Equals`: у уничтоженного `UnityEngine.Object` равенство идёт по instanceID и с живостью не связано. От Unity реестр не зависит и потому проверяется в `fast-tests`, а не только в плеере.

Вместе с ключом реестр помнит **владельца** инстанса: `TryTakeAll(key, owner)` отдаёт только созданное этим владельцем, и ключ уходит из учёта, лишь когда чужих инстансов не осталось. Перегрузка без владельца (`TryTakeAll(key)`) остаётся для `Dispose` провайдера, где живых владельцев уже нет ни у одного ключа.

`ReleaseInstance` снимает учёт **и с уже уничтоженного инстанса**: объекты умирают и мимо провайдера (`Destroy` родителя, выгрузка сцены), и запись о них иначе жила бы до следующего обращения к тому же ключу — то есть до конца процесса, если ключ больше не спрашивают. Сам объект `ReleaseInstance` уничтожает, а ассет оставляет в кэше — переоткрытие окна не платит за повторную загрузку.

Мёртвые инстансы вычищаются из реестра лениво: `HasAlive` их отбрасывает, `CountAlive` только считает (снапшот обязан быть чистым чтением), `TryTakeAll` отдаёт весь набор владения вызывающему.

## Ключи Addressables

Ключ — это `m_Address` записи в группе `Assets/AddressableAssetsData/AssetGroups/*.asset`. В коде ключи живут константами фичи (`<Feature>Constants.Prefabs.*`, `IconConstants.Formats.AtlasName`), не литералами по месту. Как называется ключ и как он связан с именем файла ассета — [[Naming]], секция «Имена ассетов».

Ключи view (`[AutoWindow]` / `[AutoPopup]`) живут в трёх местах независимо — константа, атрибут и запись в Addressables. Первые два сверяет генератор (`ADG003` / `ADG004`), запись в Addressables — EditMode-тест `AddressableKeyTests`; без него отсутствующий address падал бы в рантайме в фазе `Load`.

Тот же тест сверяет с адресами и ключи, которые код передаёт в `IAssetProvider` строкой: `AssetKeyConstants_HaveAddressablesEntry` собирает рефлексией `string`-константы из вложенных классов-держателей ключей (`Prefabs`, `Configs`, `Canvases`, `Sounds`, `Music`, `Atlases`) и из классов `*Keys` (`MusicKeys`, `SoundKeys`). Список держателей — опт-ин: рядом живут классы с ключами локализации, именами аналитики и форматами, адресами они не являются. Новый держатель ключей добавляется в `KeyHolderNames`, иначе он выпадет из проверки.

Обратное направление тест не проверяет: адрес без константы не ошибка — так адресуются сцены, префабы из сериализованных ссылок и ассеты, ключ которых собирается форматом из данных.

## Кто уже грузит ассеты

| Потребитель | Что грузит | Как освобождает |
| --- | --- | --- |
| `AutoViewEntity` | префабы view в фазе `Load` через собственный `IAssetScope` | `_assets?.Dispose()` в `Dispose`; гейт мог пропустить фазы — тогда scope-а нет |
| `ViewFactory` | `InstantiateAsync` view под нужный canvas **через `IAssetScope` вызывающего** | тот же scope, что владеет ключом префаба |
| `IconProvider` | спрайты и атласы, не-persistent | по шторке; в `Dispose` — `ReleaseAsset` по своим ключам |
| `AudioClipLoader` | `AudioClip`, `persistent` по параметру | вызывающий |
| `ConfigResolver` | dummy-json конфигов (`TextAsset`) | `ReleaseAsset` сразу после чтения текста |
| `CanvasProvider` | префабы канвасов | по времени жизни канвасов |
| `DailyBonusCore` | префабы дней через `IAssetScope` | `_assets.Dispose()` в `Dispose` фичи |

## Диагностика

`IAssetProviderDiagnostics.GetSnapshot()` отдаёт `AssetProviderSnapshot`: закэшированные ключи с типом, persistent-флагом и числом живых инстансов, in-flight ключи, persistent-ключи, группы инстансов. Снапшот — **чистое чтение**: мёртвые инстансы он считает, но не удаляет, чтобы съём состояния не мутировал провайдер.

Потребителя у снапшота сейчас нет: дев-оверлей, который его показывал, выпилен вместе с платным SRDebugger ([[SRDebugger]]). Интерфейс оставлен как точка съёма — логировать снапшот или вывести его в свой инструмент.

## Инварианты

- `Addressables.*` вызывается только внутри `AddressableAssetProvider` и `SceneLoader` (`LoadSceneAsync`). В Features прямой вызов запрещён.
- `Features` инжектит `IAssetScopeFactory` и работает через `IAssetScope`; `IAssetProvider` — граница `Foundation`. Поэтому `persistent:` и `ReleaseAsset` в фичевом коде не встречаются вообще.
- Провайдер — `Lifetime.Singleton`; второй реализации `IAssetProvider` в рантайме нет.
- Один ключ — один тип за жизнь кэша; смена типа для того же ключа считается ошибкой, а не поводом перезагрузить.
- `ReleaseInstance` не освобождает ассет; `ReleaseAsset` не трогает persistent-ключи и ключи с живыми инстансами.
- Безусловных релизов нет: `ReleaseCompletely` адресован паре «ключ + владелец» и освобождает handle, только когда владельцев не осталось. Единственное исключение — `Dispose` самого провайдера.
- Инстанс уничтожает только его владелец; заявку на persistent снимает только тот, кто её поставил.
- Инстанс view принадлежит тому же владельцу, что и ключ префаба: `IViewFactory.CreateView` требует `IAssetScope owner` без умолчания.
- Ключ, который держит живой scope, переживает шторку — она отпускает только владение корневого владельца.
- Загрузка, которую к моменту завершения никто не ждёт, не кэшируется: handle освобождается сразу.
- Загрузка через задиспоженный scope — `ObjectDisposedException`, а не тихая работа мимо трекинга. Повторный `Dispose` — no-op.
- Неуспешная загрузка не кэшируется: handle освобождается, ключ уходит из in-flight.
- Ассет, переживший шторку, — всегда осознанный `persistent: true` или живой владелец, а не забытый релиз.
- Отменённый токен не пропускает ни выдачу из кэша, ни создание инстанса.
- Инстанс уходит из учёта в момент `ReleaseInstance` независимо от того, жив он ещё или уничтожен снаружи.

## Как расширять

**Другой бэкенд загрузки** (Resources, AssetBundle напрямую): реализовать `IAssetProvider`, снять `[AutoRegistration]` с `AddressableAssetProvider` и зарегистрировать новую реализацию `Lifetime.Singleton`. Контракт достаточно узкий, чтобы `AssetScope` переиспользовался как есть — он работает поверх любого `IAssetProvider`.

**Предзагрузка набора ассетов** (варм-ап сцены): отдельный `LifecycleEntity` с фазой `Load`, грузящий ключи с `persistent: true`. Отдельного API для батча в провайдере нет и заводить его без потребности не нужно — `UniTask.WhenAll` по `LoadAssetAsync` закрывает случай.

**Пул инстансов**: не в провайдере. Провайдер отвечает за handle и учёт, пул — слой выше, и писать его руками не нужно: у Unity есть `UnityEngine.Pool` (`ObjectPool<T>`, `ListPool<T>`, `GenericPool<T>`).

## Тесты

- `Assets/Framework/Foundation/Tests/AssetOwnershipTests.cs` — владение ключом: релиз одного владельца при живом втором, идемпотентный `Acquire`, релиз чужим владельцем, заявка на persistent и её снятие.
- `Assets/Framework/Foundation/Tests/AssetScopeTests.cs` — поведение scope: делегирование, релиз каждого ключа в `Dispose`, дедуп ключей, `AssetReference` → runtime-ключ, идемпотентный `Dispose`, `ObjectDisposedException`, независимость вложенного scope, передача себя владельцем и релиз только своего владения при общем ключе двух scope-ов.
- `Assets/Framework/Foundation/Tests/AutoViewEntityTests.cs` — декоратор грузит и релизит через свой scope: после `Dispose` ключи уходят в `ReleaseCompletely`, view создаётся через тот же scope, а при пропущенных гейтом фазах не релизится ничего.
- `Assets/Framework/Foundation/Tests/InflightLoadsTests.cs` — дедупликация незавершённых загрузок: несколько одновременных ожидающих получают результат, исключение раздаётся всем, несовпадение типа бросается до ожидания, отмена одного ожидающего не мешает остальным, запись уходит из in-flight после завершения.
- `Assets/Framework/Foundation/Tests/InstanceRegistryTests.cs` — учёт инстансов: снятие мёртвого инстанса, идентичность по ссылке, ленивая чистка в `HasAlive` против чистого `CountAlive`, передача владения в `TryTakeAll` и выборка инстансов конкретного владельца.
- `Assets/Framework/Foundation/Tests/AssetInstantiationTests.cs` — текст ошибки об отсутствующем компоненте.
- `Assets/Framework/Foundation/Tests/IconProviderTests.cs` — кэш иконок и реакция на шторку.
- `Assets/Framework/Features/Tests/AddressableKeyTests.cs` — ключи view и константы-ключи ассетов против авторинга Addressables.

Фейк — `Assets/Framework/Foundation/Tests/Fakes/FakeAssetProvider.cs`: реализует и `IAssetProvider`, и `IAssetOwnerHost`, записывает `LoadedKeys` / `PersistentKeys` / `InstantiatedKeys` / `ReleasedAssets` / `ReleasedCompletely` плюс пары «ключ + владелец» (`LoadedByOwner`, `InstantiatedByOwner`, `ReleasedCompletelyByOwner`) и возвращает `null` вместо ассетов. `CreateScope()` у него отдаёт настоящий `AssetScope` — сам scope тестируется поверх фейка, без Unity.

Сам `AddressableAssetProvider` тестами не покрыт: `Addressables.LoadAssetAsync` и `Object.Instantiate` требуют плеера (он же — строка в `Tools/mutation-check.exceptions.txt`). Поэтому логика, которую есть смысл проверять, вынесена в тестируемые `AssetOwnership`, `AssetScope`, `AssetInstantiation` и `InstanceRegistry` — в провайдере остаётся адаптер к Addressables и порядок вызовов.

## Когда обновлять

- Изменился контракт `IAssetProvider` / `IAssetScope` / `IAssetScopeFactory` или семантика `persistent` / релизов.
- Сдвинулась граница слоёв: фича начала инжектить `IAssetProvider` или `Foundation`-сервис перешёл на scope.
- Сменился триггер группового освобождения (сейчас — `LoadingCurtainShownSignal`).
- Изменились правила трекинга в `AssetScope` или отношение вложенных scope-ов.
- Появилась вторая реализация `IAssetProvider` или другой бэкенд загрузки.
- Изменился состав `AssetProviderSnapshot` или дебаг-оверлей.
- Изменились правила учёта инстансов в `InstanceRegistry` или правила владения ключом (`AssetOwnership`, `IAssetOwnerHost`).
- Появился третий вид владельца или владелец протёк в публичный `IAssetProvider`.

## Last Verified

2026-08-26, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Assets-Addressables") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Assets-Addressables") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```