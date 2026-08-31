# AGENTS.md

Всегда загруженный контракт агента для этого репозитория. Детали архитектуры и процессов лежат в
`unity-my-template-docs/`; открывай только статью, релевантную текущей задаче.

## Проект

- Unity 6000.3.14f1. Рантайм-код: `Assets/Framework/Foundation/`,
  `Assets/Framework/Features/`, `Assets/Framework/Integrations/`.
- Стек: VContainer, UniTask, R3, MemoryPack, PrimeTween, ZLinq.
- Это шаблон: общая обратная совместимость не требуется. Исключение — сохранённые схемы
  `SaveBlob`; для них действуют правила `unity-my-template-docs/Architecture/SaveLoad.md`.
- Коммиты и push делает пользователь. Коммить только по явной просьбе.
- Сохраняй предсуществующие и не относящиеся к задаче изменения рабочего дерева.

## Жёсткие границы

- Направление зависимостей: `Features` -> `Foundation`; зависимость `Foundation` -> `Features`
  запрещена.
- `.csproj` и `.sln` генерирует Unity; не правь их.
- Логируй через `ILogChannel` / `LogChannel<T>`, не через `Debug.Log*`.
- Загружай ассеты через `IAssetProvider` / `IAssetScope`. Прямой `Addressables.*` разрешён только в
  `AddressableAssetProvider` и `SceneLoader` внутри Foundation.
- Новые сериализуемые члены `SaveBlob` добавляй в конец. Существующие члены не удаляй и не
  переставляй; перед изменением схемы прочитай `unity-my-template-docs/Architecture/SaveLoad.md`.
- Логика в Model, ViewModel, utilities и Foundation-сервисах начинается с падающего assertion.
- Получай зависимости через DI. Static singleton, `Find*ByType`, `GameObject.Find` и
  `IObjectResolver.Resolve` допустимы только в composition root.
- Отдавай наружу неизменяемое состояние: read-only reactive types, read-only collections,
  immutable values. Mutable collections, массивы, `ReactiveProperty`, `Subject`, public fields и
  public events остаются private.
- Имена: без `Manager`, `Helper`, `Utils`, `Handler`; атрибут заканчивается на `Attribute`; сигнал —
  на `Signal` и не начинается с `On`.
- Для Inspector используй штатные атрибуты Unity, для диагностики — проектный logger. Не добавляй в
  шаблон платные Asset Store плагины.

## Порядок работы

1. До правки найди корневую причину. Если корневой фикс вне запроса, сделай узкий безопасный фикс и
   сообщи, какой дополнительный скоуп нужен.
2. До первой правки репозитория перечисли целевые пути и запиши их в
   `.agent-state/ScopeCheck/declared.txt`. Не включай туда чужие baseline-изменения.
3. Изменения кода, ассетов, tooling или документации требуют тикет в
   `unity-my-template-docs/Tasks/`; анализ и ревью — нет. Скопируй соответствующий шаблон `*-1.md`
   и поставь `status: In Progress` до реализации.
4. Прочитай релевантную документацию, выполни заявленный скоуп и поддерживай тикет актуальным.
5. Выполни соразмерные проверки и перечитай diff. Работа готова, только когда доступные проверки
   зелёные; разделяй static checks, проверку Unity Editor и пользовательскую приёмку.

До кода предложи письменный план и дождись одобрения, если изменение пересекает несколько фич или
архитектурных слоёв, меняет публичный interface/event/data format, добавляет зависимость, меняет
lifecycle или требует существенного trade-off. В остальных случаях сформулируй разумное допущение
и продолжай. Точное решение пользователя закрывает пространство решений, кроме случаев конкретного
вреда.

## Маршрутизация документации

Начни с `unity-my-template-docs/01-Agent-Navigation.md`. В выбранной статье прочитай `Для агента`,
`Инварианты`, `Как расширять` / `Как добавить`, `Когда обновлять`, затем проверь `source_paths`. При
расхождении код — источник истины; описывающую изменённое поведение статью обнови вместе с кодом.

Основные ветки:

- Архитектура, naming, DI, signals, public state: `unity-my-template-docs/Architecture/Naming.md`,
  `Class-Interaction.md`, `Foundation-vs-Features.md` в той же папке.
- Lifecycle, scenes, scopes, source generator:
  `unity-my-template-docs/Architecture/Initialization-LifecycleEntity.md`.
- UI и MVVM: `unity-my-template-docs/Architecture/UI-Views.md`, `UI-MVVM.md` и
  `unity-my-template-docs/Recipes/Add-UI-Window.md`.
- Assets, save, configs, time, logging: одноимённая статья в `unity-my-template-docs/Architecture/`.
- Tests и TDD: `unity-my-template-docs/Architecture/Testing-TDD.md`.
- Tickets, hooks, skills, Unity CLI: одноимённая статья в `unity-my-template-docs/Process/`.

После изменения документированного поведения запусти `powershell -File Tools/docs-coverage.ps1`.
Если обновление статьи не требуется, объясни почему в финальном ответе.

## Тикеты и приёмка

Источник истины — `unity-my-template-docs/Process/Tickets.md`. Обязательный минимум:

- `related` непустой и ведёт на статьи vault; `module` совпадает хотя бы с одной связанной статьёй.
  Запусти `powershell -File Tools/ticket-format.ps1`.
- Одновременно `In Progress` не больше трёх feature/bug; epics не считаются.
- Проверки агента — обычные пункты. Проверки, которые закрывает только пользователь, помечай
  `- [ ] @user ...`.
- Оставляй тикет `In Progress`, пока пользователь не подтвердил компиляцию и результат. Запуск
  тестов агентом сам по себе не является пользовательской приёмкой.

Когда открыты только `@user`-пункты, задай ровно два варианта через доступный structured-input
tool; если его нет — тем же коротким вопросом в тексте:

1. `Да, всё в порядке — закрываем тикет`
2. `Ещё не проверял — вернусь позже`

После первого ответа закрой все `@user`-пункты, поставь `status: Done`, обнови `updated`. После
второго ничего не меняй. Замечания свободным текстом внеси новыми пунктами без `@user` и продолжай.

## Проверки и TDD

- Быстрая компиляция и EditMode tests: `powershell -File Tools/fast-tests.ps1`.
- Для Model/ViewModel/utilities/Foundation logic: добавь assertion, падающий на отсутствующем
  поведении, запусти red, внеси минимальную реализацию, запусти green. Crash или отсутствующий method
  не считается red. Следуй `unity-my-template-docs/Architecture/Testing-TDD.md` и
  `Tools/tdd-check.ps1`.
- После green-цикла нетривиальных branches, boundaries, накопления состояния или нового
  Foundation-сервиса запусти `powershell -File Tools/mutation-check.ps1`; surviving mutants разбери
  вручную.
- Релевантные гейты: `naming-check.ps1`, `interaction-check.ps1`, `generator-tests.ps1`,
  `generator-hash.ps1`, `docs-coverage.ps1`, `scope-check.ps1`, `ticket-format.ps1`.
- Claude Code регистрирует цепочку в `.claude/settings.json`. Codex регистрирует SessionStart и
  последовательный Stop-hook в `.codex/hooks.json`; project-local Codex hooks нужно доверить через
  `/hooks`. Любую проверку можно запустить вручную.
- Когда Unity Editor открыт, используй Unity CLI для recompile, console и Test Runner. Подтверждение
  пользователя остаётся гейтом закрытия тикета.

## Unity CLI

Перед работой с живым Editor прочитай `unity-my-template-docs/Process/Unity-CLI.md`.

1. Запусти `unity status`; продолжай только при `state: ready`.
2. Получи актуальные имена и схемы через `unity list --json`; параметры не угадывай.
3. Вызывай `unity cmd <tool> --<parameter>=<value> --json` и проверяй `.data.result`, а не только
   exit code или `success`.
4. Основные tools: `recompile_status`, `get_console_logs`, `run_tests`, `test_status`.

Unity CLI дополняет `Tools/fast-tests.ps1`, но не заменяет его и пользовательскую приёмку.

## Unity-ассеты и ручные шаги

- Перемещай или удаляй Unity-ассет вместе с `.meta`. Для нового файла `.meta` генерирует Unity.
- Прямая YAML-правка ограничена существующими сериализованными полями. Иерархия, новые components
  и object references настраиваются вручную в Inspector.
- В финальном ответе перечисляй только недоступные агенту ручные шаги: точный asset/scene, object или
  component, последовательность действий и ожидаемый результат.
