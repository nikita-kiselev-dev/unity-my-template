---
title: Ads
type: architecture
area: Foundation
module: Ads
status: actual
source_paths:
  - Assets/Framework/Foundation/Ads/IAdsController.cs
  - Assets/Framework/Foundation/Ads/AdsController.cs
  - Assets/Framework/Foundation/Ads/AdsPolicy.cs
  - Assets/Framework/Foundation/Ads/IAdsProvider.cs
  - Assets/Framework/Foundation/Ads/AdFormat.cs
  - Assets/Framework/Foundation/Ads/AdResult.cs
  - Assets/Framework/Foundation/Ads/AdsConstants.cs
  - Assets/Framework/Foundation/Ads/Data/AdsData.cs
  - Assets/Framework/Foundation/Ads/Configs/AdsConfig.cs
  - Assets/Framework/Foundation/Ads/Configs/IAdsConfig.cs
  - Assets/Framework/Foundation/Ads/Providers/NullAdsProvider.cs
  - Assets/Framework/Foundation/Ads/Signals/AdStartedSignal.cs
  - Assets/Framework/Foundation/Ads/Signals/AdFinishedSignal.cs
  - Assets/Framework/Foundation/Ads/Stub/EditorAdsProvider.cs
  - Assets/Framework/Foundation/Ads/Stub/IAdsStubHost.cs
  - Assets/Framework/Foundation/Ads/Stub/AdsStubPopupHost.cs
  - Assets/Framework/Foundation/Ads/Stub/ViewModel/AdsStubPopupViewModel.cs
  - Assets/Framework/Foundation/Ads/Stub/View/AdsStubPopupView.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Registrators/Ads/AdsScopeRegistrator.cs
  - Assets/Framework/Foundation/Initialization/Scripts/Registrators/Ads/AdsScopeRegistrator.Yandex.cs
  - Assets/Scripts/YandexGames/YandexAdsProvider.cs
  - Assets/Framework/Foundation/Audio/IAudioController.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Time]]"
  - "[[SaveLoad]]"
  - "[[UI-Views]]"
tags:
  - architecture
  - foundation
  - ads
  - monetization
  - live-ops
updated: 2026-08-16
---

# Ads

## Для агента

Открывай эту статью, если фиче нужно показать рекламу, спросить её доступность или подключить реальную рекламную сеть.

Главное правило: фича знает только `IAdsController` — фасад с форматами `Banner` / `Interstitial` / `Rewarded`. Ни одна фича не обращается к SDK напрямую и не считает кулдаун сама.

Второе правило: **исход показа всегда один** — `AdResult`. `NotReady` означает «показывать нечем» (формат выключен, кулдаун, сеть не готова, идёт другой показ) и не является ошибкой: контроллер не бросает даже при выключенной конфигом рекламе.

## Назначение

Единый контракт рекламы в `Foundation` + сменный провайдер SDK. Контроллер держит правила (доступность, кулдаун, счётчики, mute звука, сигналы ad-сессии), провайдер — только вызовы конкретной сети.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `IAdsController` / `AdsController` | Фасад для фич. `LifecycleEntity`, `Lifetime.Singleton`, фаза `Init` на Bootstrap-сцене. |
| `AdsPolicy` (`internal`) | Правила без Unity: доступность формата, кулдаун, учёт просмотров. Время — параметром. |
| `IAdsProvider` | Контракт сети. Активная реализация всегда одна. |
| `NullAdsProvider` | Дефолт в билде без сети: `IsReady` = false, показ = `NotReady`. |
| `EditorAdsProvider` + `AdsStubPopupHost` | Заглушка редактора: попап с кнопками Success/Fail. |
| `AdsData` | Сейв: только счётчики просмотров. |
| `AdsConfig` | Конфиг: включённость форматов, кулдаун interstitial. |
| `AdStartedSignal` / `AdFinishedSignal` | Границы ad-сессии для геймплея. |

## Как показать рекламу из фичи

```csharp
[Inject] private readonly IAdsController _ads;

// Кнопка «посмотреть за награду»: активность биндится на готовность.
_ads.IsReady(AdFormat.Rewarded).Subscribe(isReady => button.interactable = isReady).AddTo(this);

// Синхронный вызов с колбэками.
_ads.Show(AdFormat.Rewarded, onSuccess: () => _inventory.Add(reward));

// Или из async-кода — тот же путь исполнения.
var result = await _ads.ShowAsync(AdFormat.Interstitial, ct);
```

`Show(...)` внутри делает `ShowAsync(...).Forget()` и разбирает результат: гарантия «ровно один исход» живёт в одном месте.

`IsReady(format)` — это «готово к показу прямо сейчас»: конъюнкция `IsEnabled` формата в конфиге, рантайм-флага `SetFormatEnabled`, `provider.IsReady(format)`, истёкшего кулдауна и отсутствия активного показа. Значение пересчитывается при каждом изменении состояния контроллера и по тику `IClock.ServerNow` (раз в секунду): единого события «готовность изменилась» у сетей нет, а точность до секунды кнопке достаточна.

## Ad-сессия

Вокруг вызова провайдера контроллер поднимает `IsAdPlaying`, мьютит звук (`IAudioController.SetMuted`) и триггерит `AdStartedSignal` / `AdFinishedSignal`. Без этого на WebGL игра продолжает идти и звучать под рекламой.

`Time.timeScale = 0` контроллер **не** ставит: это заморозило бы `UnityTimeProvider`, на котором крутятся `ServerNow`, `Countdown` и автосейв, и убило бы PrimeTween-анимации. Пауза геймплея — задача потребителя через `IsAdPlaying`.

Баннер сессией не считается: `ShowAsync(AdFormat.Banner)` зовёт `provider.SetBannerVisible(true)` и возвращает `Success`, не мьютя звук и не увеличивая счётчики. Скрывается баннер через `SetFormatEnabled(AdFormat.Banner, false)`.

Исключение из провайдера превращается в `AdResult.Failed` и пишется в `Logger.LogError`, наружу не пробрасывается: `Show(...)` — fire-and-forget, и упавший SDK не должен ронять вызывающий код.

## Кулдаун и счётчики

Кулдаун есть только у `Interstitial`, и складывается он из двух независимых таймеров:

- `interstitial_cooldown_seconds` — от предыдущего показа;
- `interstitial_session_start_cooldown_seconds` — от старта сессии (моменты фазы `Init` на Bootstrap-сцене), чтобы первые секунды игры не начинались с рекламы.

Дедлайн — **позднейший из двух**, поэтому показ внутри session-start окна не укорачивает его. Время берётся из `IClock.ServerUtcNow`.

Остаток кулдауна доступен двумя способами: `InterstitialCooldown()` — тикающий `Observable<TimeSpan>` для UI-таймера (снапшот дедлайна на момент вызова, завершается на нуле, после показа нужна новая подписка) и `InterstitialCooldownLeft` — снапшот для проверок и дебага, где подписка избыточна.

Момент последнего показа живёт **только в рантайме** (`AdsPolicy`), в сейве его нет: между сессиями его роль выполняет session-start кулдаун. В `AdsData` сохраняются лишь счётчики `InterstitialWatched` / `RewardedWatched`, и растут они только на `AdResult.Success`.

Успешный rewarded **перезапускает** кулдаун interstitial, если включён `rewarded_resets_interstitial_cooldown`: игрок, только что посмотревший рекламу добровольно, не должен сразу получить принудительную.

## Конфиг

`AdsConfig` (`Assets/Framework/Foundation/Ads/Content/Json/AdsConfig.json`), поля:

```json
{
  "is_enabled": true,
  "banner_enabled": true,
  "interstitial_enabled": true,
  "rewarded_enabled": true,
  "interstitial_cooldown_seconds": 60,
  "interstitial_session_start_cooldown_seconds": 10,
  "rewarded_resets_interstitial_cooldown": true
}
```

`is_enabled: false` выключает всю рекламу: `AdsController` инжектит `AdsConfig`, поэтому `LifecycleGate` не пропустит его ни в одну фазу. Объект при этом существует и инжектится в фичи — все вызовы возвращают `NotReady`, показов и обращений к провайдеру нет.

Рантайм-флаг `SetFormatEnabled` может формат только **запретить**: включить то, что выключено конфигом, он не может.

## Заглушка редактора

`EditorAdsProvider` (Singleton) держит ссылку на `IAdsStubHost`, которую ставит Scoped `AdsStubPopupHost` в своём `Init` и снимает в `Dispose`. Нет хоста (Bootstrap-сцена) — показ отдаёт `NotReady`, а не падает.

Хост обязан быть Scoped: `SceneStarter` прогоняет фазы заново на каждой сцене, а wrapper `[AutoPopup]` вешается один раз и релизит только последний созданный view — Singleton-хост после первой смены сцены держал бы уничтоженный попап.

Исходы заглушки:

- кнопка **Success** → `Success`;
- кнопка **Fail** (видна только для `Rewarded`) → `Failed`;
- закрытие кликом по фону (`PopupBackgroundClickedSignal` → `ViewRouter.CloseLast()`) → `Skipped`;
- исход выставляется ровно один раз (`UniTaskCompletionSource.TrySetResult`), повторные нажатия игнорируются.

Подписка на `OnClosed` живёт ровно одну сессию показа (`using` внутри `ShowAsync`): `OnClosed` приходит после анимации закрытия, и «опоздавшее» событие не должно закрыть следующий показ.

`AdsStubPopupView` и `AdsStubPopupViewModel` компилируются **всегда**, не под `#if UNITY_EDITOR`: `MonoView`-компонент нельзя вырезать из билда, иначе prefab потеряет скрипт, а generic-параметр view тянет за собой VM. Под `#if UNITY_EDITOR` живут только `EditorAdsProvider`, `IAdsStubHost` и `AdsStubPopupHost` — то, что регистрируется в контейнере.

Ручная проверка — редактор: `EditorAdsProvider` показывает попап-заглушку с кнопками Success/Fail,
исход пишется каналом `Ads` в консоль. Дев-оверлея у шаблона больше нет ([[SRDebugger]]).

`NotReady` при этом не виден нигде: `ShowAsync` отдаёт его ранним `return` до лога исхода
(`AdsController.cs:98`). Понадобится наблюдать отказы — логировать их там, а не поднимать оверлей.

## Регистрация

`AdsScopeRegistrator.Configure(builder)` вызывается из `RootScope` и выбирает **одну** реализацию `IAdsProvider`:

1. `RegisterPlatform(builder, ref registered)` — partial-метод платформенного адаптера под define;
2. иначе `EditorAdsProvider` под `#if UNITY_EDITOR`;
3. иначе `NullAdsProvider`.

Регистратор, а не `[AutoRegistration]` на каждом провайдере: два типа под одним интерфейсом дали бы недетерминированный резолв. Коллекция провайдеров (как у `IAnalyticsService`) не нужна: аналитика веерная, реклама — одна активная сеть.

## Инварианты

- Прямой вызов SDK рекламы из `Features` запрещён — только `IAdsController`.
- Ни один метод `IAdsController` не бросает: недоступность выражается через `AdResult.NotReady`.
- Активная реализация `IAdsProvider` ровно одна, выбирает её `AdsScopeRegistrator`. Провайдер из
  `Assembly-CSharp` регистрирует себя сам, но только вместе с `registered = true` в регистраторе.
- `AdsController` — Singleton и поэтому **не инжектит** `IViewRouter` / `IViewFactory` (они Scoped): это captive dependency, её ловит `RegistrationGraphTests.Graph_DoesNotCaptureScopedDependencies_InRootSingletons`.
- Кулдаун и счётчики меняются только через `AdsPolicy.RegisterShown`, время туда приходит параметром.
- В `AdsData` не попадает время: даты показов — рантайм-состояние политики.
- Время берётся только из `IClock`; `DateTime.UtcNow` в модуле запрещён.
- Счётчик растёт только на `Success`.

## Как подключить реальную сеть

1. Отдельный asmdef в `Assets/Framework/Integrations/<Network>/`, зависящий от `Foundation` и пакета сети.
2. Реализация `IAdsProvider`: инициализация SDK, готовность форматов, показ, видимость баннера.
3. Partial-часть регистратора рядом с `AdsScopeRegistrator.cs` (по образцу `LiveOpsScopeRegistrator.GamePush.cs`):

```csharp
public static partial class AdsScopeRegistrator
{
#if MY_NETWORK_ENABLED
    static partial void RegisterPlatform(IContainerBuilder builder, ref bool registered)
    {
        builder.RegisterSingleton<MyNetworkAdsProvider>();
        registered = true;
    }
#endif
}
```

4. Define включается в Project Settings → Player → Scripting Define Symbols.

Правила и UI при этом не меняются: кулдаун, счётчики, mute и сигналы уже в контроллере.

## Yandex Games: сеть без asmdef

Плагин `PluginYourGames` не имеет ни одного `.asmdef`, значит `YG2` живёт в `Assembly-CSharp`, и
asmdef-сборка на него сослаться не может — та же ситуация, что у языка ([[Localization]]).
Поэтому у подключения сети есть **вторая форма**: провайдер лежит в `Assets/Scripts/<Платформа>/`
и регистрирует себя сам через `[AutoRegistration]`, а `AdsScopeRegistrator.RegisterPlatform`
только выставляет `registered = true`, чтобы под `IAdsProvider` не оказалось двух типов.

`YandexAdsProvider` (`Assets/Scripts/YandexGames/`), дефайны
`UNITY_WEBGL && PLUGIN_YG_2 && InterstitialAdv_yg && RewardedAdv_yg`:

| Что | YG2 | Исход |
| --- | --- | --- |
| Interstitial | `InterstitialAdvShow()` | `onCloseInterAdv` → `Success` (любое закрытие), `onErrorInterAdv` → `Failed` |
| Rewarded | `RewardedAdvShow(id)` | `onRewardAdv` до закрытия → `Success`, закрытие без награды → `Skipped`, `onErrorRewardedAdv` → `Failed` |
| Banner | — | `IsReady` = false, `SetBannerVisible` — no-op; модуль `StickyAdv_yg` не подключён |

У interstitial исход `Skipped` не встречается: награды за него нет, а отличать «досмотрел» от
«закрыл сам» нужно только ради награды. Флаг `wasShown` не используется — Яндекс отдаёт в нём
`false` и на фактически показанной рекламе, и такой показ не попадал бы ни в счётчик, ни в
кулдаун. У rewarded ровно наоборот: `Success` только по `onRewardAdv`, закрытие без награды —
`Skipped`.

`InitAsync` — no-op: готовность SDK держит `YandexSdkEntity` в фазе `Load`, и барьер между
фазами делает чтение `YG2.isSDKEnabled` в `Init` синхронным.

Показ ждёт исход с **потолком в 90 секунд** realtime и никак не реагирует на «долго не
открывается»: между `ysdk.adv.showFullscreenAdv` и колбэком `onOpen` идёт загрузка креатива, и
короткое окно ожидания открытия превращало бы уже показавшуюся рекламу в `NotReady` — счётчик не
рос бы, кулдаун не вставал бы, а реклама всё равно шла. Потолок оставлен потому,
что `YG2.InterstitialAdvShow()` умеет молча ничего не сделать (`SkipNextInterAdCall()` после
rewarded, `ysdk == null` в JS): без него ad-сессия висела бы вечно с замьюченным звуком.

Регистрация идёт только в билде (`#if !UNITY_EDITOR` на атрибуте): в редакторе остаётся
попап-заглушка с теми же исходами. Сам класс компилируется всегда — опечатка в адаптере должна
всплывать в редакторе, а не на сборке.

Отдельная настройка вне кода: в `InfoYG` (Basic) `YG2.PauseGame` умеет ставить `Time.timeScale = 0`
на время рекламы. Это ровно то, чего контроллер сознательно не делает: остановленный `timeScale`
замораживает `ServerNow`, `Countdown`, автосейв и PrimeTween. `editTimeScale` держать выключенным.

## Тесты

`Assets/Framework/Foundation/Tests/AdsPolicyTests.cs`, `AdsControllerTests.cs`, `AdsStubPopupViewModelTests.cs`; фейк — `Fakes/FakeAdsProvider.cs`; конфиги строятся Newtonsoft-ом из JSON в `Tests/FoundationTestConfigs.cs`. Roundtrip `AdsData` — в `Assets/Framework/Features/Tests/SaveBlobContractTests.cs`.

Время в тестах контроллера — реальный `Clock` с `FakeRealtimeSource` + `FakeServerTimeSource` + `FakeTimeProvider`: двигать нужно обе оси (`Advance` у realtime и у time provider), как в [[Time]].

Не тестируются: `AdsStubPopupView` и prefab (view), `AdsConfig.json` (конфиг), `AdsScopeRegistrator` (регистрацию закрывает `RegistrationGraphTests`).

## Известные ограничения

**Рестарт сбрасывает кулдаун показа** — это дизайн, а не дыра: время последнего показа не сохраняется, а на новой сессии interstitial всё равно ждёт session-start кулдаун. Если понадобится непрерываемый кулдаун между сессиями, придётся вернуть время показа в `AdsData` и мириться с переводом часов игроком (внутри сессии ход часов монотонный `Stopwatch`, но при `ClockTrust.LocalFallback` anchor берётся с локальных часов).

**Prefab заглушки попадает в билд.** `AdsStubPopup.prefab` компилируется и собирается всегда, хотя используется только в редакторе. Цена — один неиспользуемый prefab; исключение группы Addressables из билда — на усмотрение проекта.

**Суточных лимитов показов нет.** Есть только кулдаун interstitial. Лимиты, no-ads-покупка и отдельная аналитика рекламы — вне текущего скоупа.

## Когда обновлять

- Появился новый член `IAdsController` или `IAdsProvider`.
- Изменились правила доступности, кулдауна или учёта просмотров.
- Появилась реальная реализация `IAdsProvider` в `Integrations/` или в `Assets/Scripts/<Платформа>/`.
- Изменилась схема `AdsData` или набор полей `AdsConfig`.
- Изменилось поведение заглушки редактора или её попапа.

## Last Verified

2026-08-16, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Ads") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Ads") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```