---
title: Localization
type: architecture
area: Foundation
module: Localization
status: actual
source_paths:
  - Assets/Framework/Foundation/Localization/Scripts/Controller/ILocalizationController.cs
  - Assets/Framework/Foundation/Localization/Scripts/Controller/LocalizationController.cs
  - Assets/Framework/Foundation/Localization/Scripts/ILocaleSource.cs
  - Assets/Framework/Foundation/Localization/Scripts/Extensions/LocalizationExtensions.cs
  - Assets/Framework/Foundation/Localization/Scripts/LocalizationConstants.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Testing-TDD]]"
tags:
  - architecture
  - foundation
  - localization
updated: 2026-08-15
---

# Localization

## Для агента

Открывай эту статью, если нужно достать локализованный текст, добавить язык или научить игру
брать язык откуда-то извне (платформа, лаунчер, настройки игрока).

Главное правило: **язык выбирается один раз в `Init` на Bootstrap-сцене**, дальше игра работает
с уже выставленной локалью Unity Localization. Логика «откуда взялся код языка» и логика «какая
локаль ему соответствует» — разные вещи и живут в разных типах.

## Назначение

Слой поверх пакета `com.unity.localization`: инициализация пакета в фазе `Load`, выбор стартовой
локали в фазе `Init` и короткий доступ к строкам таблиц.

Таблицы и локали лежат в `Assets/Framework/Foundation/Localization/Content/`; на 2026-08-15
заведены две локали — `English (en)` и `Russian (ru)`.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `LocalizationController` | `LifecycleEntity` Bootstrap-сцены: ждёт инициализацию пакета, ставит стартовую локаль |
| `ILocaleSource` | откуда взялся код языка игрока: платформа, лаунчер, системные настройки |
| `ILocalizationController` | граница фичи; на 2026-08-15 пустой |
| `LocalizationExtensions` | `"key".Localize()` → `UniTask<string>` из таблицы |
| `LocalizationConstants.Tables` | имена таблиц (`General`) |

## Откуда берётся стартовый язык

Проектного кода тут ровно одна обязанность — **достать код языка**: `ILocaleSource.TryGetLocaleCode()`,
синхронный, `Result<string>`; отсутствие языка (SDK не поднялся, платформы нет) — штатный исход.

Сопоставление кода с локалями проекта своего типа **не требует**: его целиком делает
`LocalizationSettings.AvailableLocales.GetLocale(code)` — регистронезависимое сравнение
(`LocaleIdentifier.Equals` → `OrdinalIgnoreCase`), фолбэк по цепочке `CultureInfo.Parent`
(`ru-RU` → `ru`, `zh-Hans-CN` → `zh-Hans` → `zh`) и отсев `PseudoLocale`. Собственный резолвер
здесь был бы строго хуже: отрезание по первому дефису пропускает промежуточные культуры.

`LocalizationController` инжектит `IReadOnlyList<ILocaleSource>` и берёт первый источник, который
вернул язык. Коллекция законно пуста: `ILocaleSource` внесён в `_optionalCollectionElements`
(`RegistrationGraphTests.cs:32`) как точка расширения. Пустая коллекция = локаль не трогаем, и
выбор остаётся за Locale Selectors самого пакета.

Платформенные источники живут **вне** `Foundation` и вне asmdef вовсе — в `Assets/Scripts/`
(`Assembly-CSharp`): YG2 объявлен там, а asmdef-сборка на предопределённую сослаться не может.
Регистрация всё равно автоматическая: `Assembly-CSharp` ссылается на `Foundation`, значит её
`[AutoRegistration]`-типы попадают в скан (`AutoTypeScanner.cs:51`).

## Инварианты

- Инициализация пакета (`LocalizationSettings.InitializationOperation`) ждётся в фазе `Load`, а не
  в `Init`. Любое чтение таблиц до этого момента — гонка.
- Стартовая локаль ставится в `Init`. Это работает потому, что между `Load` и `Init` стоит
  глобальный барьер (`SceneStarter.cs:99`): все `Load` всех сущностей закрыты до первого `Init`.
  Поэтому источник, которому нужен внешний SDK, обязан дождаться его в своей фазе `Load` —
  не в момент чтения языка.
- `LocalizationSettings` читается только когда источник действительно дал язык. Это не
  оптимизация: статика пакета недоступна вне Unity, и безусловное обращение ломает тесты
  контроллера.
- Сопоставление кода с локалью не пишется руками — только `AvailableLocales.GetLocale(code)`.
- Ключи таблиц берутся из `LocalizationConstants`, строковые литералы в фичах не пишутся.

## Namespace

`Framework.Foundation.Localization` и вложенные (`.Controller`, `.Extensions`).

## Как добавить источник языка

1. Реализовать `ILocaleSource` там, где доступен нужный SDK. Для платформенных плагинов без
   asmdef это `Assets/Scripts/<Платформа>/`, для всего остального — `Foundation` или отдельный
   asmdef в `Integrations/`.
2. Повесить `[AutoRegistration(Lifetime.Singleton)]`. Singleton обязателен: источник инжектится в
   `LocalizationController`, который сам Singleton, — Scoped дал бы captive dependency.
3. Если язык доступен только после инициализации внешнего SDK, завести отдельную
   `LifecycleEntity`, которая ждёт SDK в фазе `Load` (пример — `YandexSdkEntity`). Ожидание
   принадлежит SDK, а не языку: те же данные нужны рекламе и сейвам.
4. Обернуть файл в `#if` по дефайнам платформы и модуля SDK. Дефайны Yandex Games
   (`PLUGIN_YG_2`, `Localization_yg` и прочие модульные) уже прописаны в WebGL-профиле
   `ProjectSettings.asset`.

## Тесты

Собственной логики, тестируемой без Unity, в слое нет — и это следствие того, что сопоставление
кода с локалью отдано пакету. Появится своя ветвящаяся логика (приоритет источников, сохранённый
выбор игрока) — она обязана уехать в отдельный чистый тип и получить тесты.

`LocalizationController` тестируется только на статус (`LocalizationControllerTests`): он дёргает
статику `LocalizationSettings`, которая вне Unity не поднимается, а `fast-tests` работает при
закрытом редакторе. Поэтому всё решаемое без Unity-статики обязано жить в отдельных чистых типах —
контроллеру остаётся присваивание. Платформенные источники в `Assembly-CSharp` не тестируются
вовсе, и по этой причине в них не должно быть ветвлений сложнее проверки готовности SDK.

## Когда обновлять

- Появился новый источник языка или изменилась политика выбора локали.
- Добавилась таблица или локаль.
- Изменился состав `LocalizationConstants`.
- Изменилось правило сопоставления кода языка с локалью проекта.

## Last Verified

2026-08-15 — по коду `Localization/Scripts/` и `Assets/Scripts/YandexGames/`.

## Тикеты по системе

```dataview
TABLE status, updated
FROM "Tasks"
WHERE module = "Localization"
SORT updated DESC
```
