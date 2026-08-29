# Unity My Template

Шаблон Unity-проекта с готовым foundation-слоем: инициализация, DI, UI-стек, сохранения,
конфиги, локализация, аудио, реклама, аналитика, время и тесты. Нужен, чтобы новый проект
начинался с написания механик, а не с инфраструктуры.

Парадигма: ООП, реактивный UI (MVVM + R3), TDD для логики.

## Оглавление

- [Требования](#требования)
- [Стек](#стек)
- [Структура проекта](#структура-проекта)
- [Что готово из коробки](#что-готово-из-коробки)
- [Архитектура кратко](#архитектура-кратко)
- [Тесты и скрипты](#тесты-и-скрипты)
- [Документация](#документация)
- [Для AI-агентов](#для-ai-агентов)

## Требования

- **Unity 6000.3.14f1** (`ProjectSettings/ProjectVersion.txt`).
- **NuGetForUnity** — уже в `Packages/manifest.json`; NuGet-зависимости лежат в
  `Assets/Packages` и закреплены в `Assets/packages.config`.
- Скрипты автоматизации — PowerShell (Windows).
- Платных Asset Store плагинов в репозитории нет (Odin Inspector и SRDebugger выпилены).
  Подсказки в инспекторе — штатные `[Header]` / `[Tooltip]` / `[ContextMenu]`.

Способ сохранения выбирается define-символом в Project Settings → Player → Scripting Define
Symbols: `PLAYER_PREFS_SAVE_ENABLED` (включён по умолчанию) — PlayerPrefs, без него — файловое
сохранение (`FILE_SAVE_ENABLED`).

## Стек

| Библиотека | Версия | Роль |
| --- | --- | --- |
| [VContainer](https://github.com/hadashiA/VContainer) | 1.16.1 | DI |
| [UniTask](https://github.com/Cysharp/UniTask) | git | async (единственный разрешённый async-стек) |
| [R3](https://github.com/Cysharp/R3) | 1.3.0 | реактивные стримы, основа MVVM и SignalBus |
| [MemoryPack](https://github.com/Cysharp/MemoryPack) | 1.21.4 | сериализация сейвов |
| [ZLinq](https://github.com/Cysharp/ZLinq) | 1.5.4 | zero-alloc LINQ |
| [PrimeTween](https://github.com/KyryloKuzyk/PrimeTween) | 1.4.12 (local tgz) | анимации |
| Addressables | 2.9.1 | загрузка ассетов и конфигов |
| Unity Localization | 1.5.12 | тексты и локали |

Дополнительно, под define-символами: PlayFab и GamePush (LiveOps-бэкенды; без них работают
оффлайн-дефолты). Адаптеры к плагинам без asmdef (сейчас Yandex Games / PluginYourGames) лежат
в `Assets/Scripts/<Платформа>/`.

## Структура проекта

```
Assets/Framework/
  Foundation/      asmdef Foundation  — переиспользуемый слой, ничего про конкретную игру
  Features/        asmdef Features    — фичи игры (ссылается на Foundation, обратной зависимости нет)
  Analyzers/       собранный source generator AutoDecorators.Generator.dll
Assets/Scripts/    тонкие адаптеры к плагинам без asmdef (Yandex Games)
Tools/             скрипты компиляции/тестов вне Unity + исходники генератора
unity-my-template-docs/   Obsidian-vault: архитектура, рецепты, тикеты
AGENTS.md          правила для AI-агентов (CLAUDE.md просто включает его)
```

Правило разделения: фича может пригодиться в другом проекте → `Foundation/`; фича про *этот*
проект → `Features/`. Namespace повторяет путь от `Assets/`. Опциональные asmdef-адаптеры к
сторонним пакетам, когда появятся, живут в `Assets/Framework/Integrations/` — `Foundation` на них
не ссылается.

Сцены (`Assets/Framework/Foundation/Scenes/Content/`): `BootstrapperScene` → `StartScene` →
`CoreScene` / `MetaScene`.

## Что готово из коробки

**Foundation:**

- **Initialization** — `LifecycleEntity` с тремя фазами, `SceneStarter`, авторегистрация типов
  в DI, `LifecycleGate`, замеры времени каждой фазы.
- **UI** — `ViewRouter` (окна и popup-ы), `ViewFactory` с pipeline-шагами настройки, MVVM-база
  (`MonoView<T>`, `BindableView<T>`, `ViewModel`), loading curtain, анимации, canvas-провайдер.
- **SaveLoad** — `SaveEnvelope`, MemoryPack-схема с версионированием и миграциями, карантин
  повреждённых сейвов, два бэкенда (файл / PlayerPrefs), синхронная запись на выходе из игры.
- **Configs** — конфиги как обычные DI-зависимости: класс `IConfig` + `[ConfigKey("key")]`, всё
  грузится одним прогревом до фаз инициализации.
- **Time** — `IClock`: синхронное серверное время на монотонном тике, местная таймзона,
  `Countdown` для UI, ресинк после background.
- **Ads** — фасад `IAdsController` (banner / interstitial / rewarded), кулдаун, заглушка
  редактора, точка подключения сети.
- **SignalBus** — `ReactiveSignalBus` поверх R3, payload-in-signal.
- **Asset** — Addressables-провайдер с кешем хендлов; фича видит только `IAssetScope`.
- **Logger** — `ILogChannel` / `[AutoLogger]` с категориями (прямой `Debug.Log` запрещён).
- Плюс: Localization, Audio, Analytics, LiveOps (offline-дефолты + точки под PlayFab/GamePush),
  ScriptableObject-настройки, `UnityLifecycleRelay`, Utilities (`Result<T>`, `EntityStatus`, FPS).

**Features (демо-фичи как примеры паттернов):** `MainMenu`, `Settings`, `Clicker`,
`DailyBonus`, `Items` (движок инвентаря/валют на `BigInteger` с реактивными количествами плюс
его UI), игровой UI-кит (`Features/UI/`), игровые scope-ы и расширения сейвов.

## Архитектура кратко

**Инициализация.** Системный компонент наследует `LifecycleEntity` и переопределяет нужные фазы:
`Load` → `Init` (обе параллельно, с барьером между фазами) →
`PostInit` (последовательно). Порядок задаётся атрибутом `[LifecycleOrderAttribute(scene, order)]`,
регистрация — `[AutoRegistration]`. `LifecycleGate` до фаз выключает сущность целиком по
конфигу или по `IConditionalEntity.ShouldRun()`.

**DI.** `RootScope` → `BootstrapScope` / `SceneScope`. `[AutoRegistration]` на классе достаточно
для регистрации; инжект — через `[Inject] private readonly` поля. Инварианты графа
регистраций покрыты тестами (`RegistrationGraphTests`).

**UI.** Слои View → ViewModel → Model → SaveBlob/Config. View пассивен, наружу из Model/VM выходят
только `ReadOnlyReactiveProperty` / `Observable` / `ReactiveCommand`, каждый `Subscribe()`
заканчивается `.AddTo(...)`.

**Декораторы.** Source generator `AutoDecorators.Generator` убирает boilerplate: `[AutoWindow]` /
`[AutoPopup]` на поле сами грузят ассет, создают view и регистрируют его в `ViewRouter`;
`[AutoLogger]` на классе выдаёт готовое свойство `Logger`. Класс должен быть `partial`.

Подробности — в статьях `unity-my-template-docs/Architecture/`.

## Тесты и скрипты

Тестовые сборки EditMode-only: `Foundation.Tests` (`Assets/Framework/Foundation/Tests/`) и
`Features.Tests` (`Assets/Framework/Features/Tests/`). Мок-фреймворков нет — ручные фейки.

```bash
powershell -File Tools/fast-tests.ps1
```

Компиляция и прогон тестов вне Unity за секунды, редактор закрывать не нужно (требует
сгенерированных `.csproj`). Финальная истина — Unity Test Runner.

```bash
powershell -File Tools/run-tests.ps1
```

Прогон через Unity в batch-режиме (редактор должен быть закрыт).

```bash
powershell -File Tools/generator-tests.ps1
```

Тесты source generator-а (`dotnet test`, snapshot вывода и диагностики).

```bash
powershell -File Tools/build-generator.ps1
```

Сборка генератора Unity-овским Roslyn и копирование DLL в `Assets/Framework/Analyzers/` —
обязательна после любой правки генератора, иначе изменения до редактора не дойдут.

Когда редактор открыт, в него можно заглянуть командой `unity` (пакет `com.unity.pipeline`):
консоль, перекомпиляция, Test Runner. Каталог — `unity-my-template-docs/Process/Unity-CLI.md`.

CI: `.github/workflows/generator-tests.yml` гоняет тесты генератора на GitHub-hosted раннере;
`unity-tests.yml` запускается вручную (EditMode-тестам нужен раннер с Unity).

## Документация

Атомарная документация — Obsidian-vault `unity-my-template-docs/`:

- [`00-Index.md`](unity-my-template-docs/00-Index.md) — индекс и правила актуальности.
- [`01-Agent-Navigation.md`](unity-my-template-docs/01-Agent-Navigation.md) — быстрый выбор
  статьи под задачу.
- [`Architecture/Foundation-vs-Features.md`](unity-my-template-docs/Architecture/Foundation-vs-Features.md) —
  границы слоёв.
- [`Architecture/Initialization-LifecycleEntity.md`](unity-my-template-docs/Architecture/Initialization-LifecycleEntity.md) —
  lifecycle и фазы.
- [`Architecture/UI-Views.md`](unity-my-template-docs/Architecture/UI-Views.md) и
  [`Architecture/UI-MVVM.md`](unity-my-template-docs/Architecture/UI-MVVM.md) — UI-стек.
- [`Architecture/SaveLoad.md`](unity-my-template-docs/Architecture/SaveLoad.md) — формат сейва и
  эволюция схемы.
- [`Architecture/Time.md`](unity-my-template-docs/Architecture/Time.md) — работа со временем.
- [`Architecture/Ads.md`](unity-my-template-docs/Architecture/Ads.md) — реклама.
- [`Architecture/Assets-Addressables.md`](unity-my-template-docs/Architecture/Assets-Addressables.md) —
  загрузка ассетов и скоупы.
- [`Architecture/Testing-TDD.md`](unity-my-template-docs/Architecture/Testing-TDD.md) — что и как
  тестируем.
- [`Recipes/Add-UI-Window.md`](unity-my-template-docs/Recipes/Add-UI-Window.md) — добавление окна
  или popup-а.
- `Tasks/` — тикеты (`UMT-Feature-N`, `UMT-Bug-N`, `UMT-Epic-N`) и доска [`Kanban.md`](unity-my-template-docs/Tasks/Kanban.md).

Если код расходится с документацией — источник истины код, статью нужно обновить вместе с ним.

## Для AI-агентов

Все обязательные правила работы с репозиторием — в [`AGENTS.md`](AGENTS.md): запреты, TDD-цикл,
границы слоёв, конвенции кода, процесс тикетов и ручные шаги в Unity. `CLAUDE.md` только
включает его через `@AGENTS.md`.

Это шаблон, обратная совместимость не поддерживается — менять можно всё. Коммиты и push делает
пользователь.
