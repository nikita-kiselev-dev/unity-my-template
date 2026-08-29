# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Никогда

Жёсткие запреты; детали — в профильных секциях ниже.

- Не вводить зависимость `Foundation` → `Features`; `Features` ссылается на `Foundation`, обратной зависимости быть не должно.
- Не править `.csproj` / `.sln` руками — их генерирует Unity.
- Не вызывать `Debug.Log*` напрямую — только `ILogChannel` / `LogChannel<T>`.
- Не вызывать `Addressables.*` напрямую — только `IAssetProvider` / `IAssetScope`. Исключение в Foundation: `AddressableAssetProvider` и `SceneLoader` (`Addressables.LoadSceneAsync`).
- Не удалять и не переставлять сериализуемые члены `SaveBlob`-классов; новые — только в конец объявления (MemoryPack).
- Не писать реализацию Model/ViewModel/сервиса с логикой без падающего теста (TDD, секция «TDD / Тесты»).
- Не переводить тикет в `Done`, пока пользователь не подтвердил компиляцию в Unity и зелёный Test Runner.
- Не использовать суффиксы `Manager` / `Helper` / `Utils` / `Handler`; не объявлять атрибут без суффикса `Attribute`; не начинать имя сигнала с `On` (секция «Наименование»).
- Не получать зависимость в обход DI: статик-синглтон, `Find*ByType`, `GameObject.Find` и `IObjectResolver.Resolve` легальны только в composition root (секция «Взаимодействие классов»).
- Не отдавать наружу изменяемое состояние: публичное поле, `public event`, `ReactiveProperty`, `Subject`, `List` / `Dictionary` / `HashSet` / массив (там же).
- Не тащить в репозиторий платные Asset Store плагины: инспекторные подсказки — штатные `[Header]` / `[Tooltip]` / `[ContextMenu]`, дев-диагностика — консоль и Unity CLI (секция «Foundation vs Features»).

## Проект

Unity 6000.3.14f1. Код в `Assets/Framework/Foundation/` (asmdef `Foundation`), `Assets/Framework/Features/` (asmdef `Features`) и изолированных адаптерах `Assets/Framework/Integrations/` — границы слоёв в секции «Foundation vs Features».

Стек: VContainer (DI), UniTask (async), R3 (реактивные стримы, поверх — `ReactiveSignalBus`), MemoryPack (сериализация), PrimeTween, ZLinq.

Быструю проверку компиляции и тестов агент выполняет сам без Unity: `powershell -File Tools/fast-tests.ps1` (секция «TDD / Тесты»). Финальная истина — Unity Editor: после правок просить пользователя подтвердить компиляцию и зелёный Test Runner. Когда редактор открыт, агент может заглянуть в него сам через `unity` CLI (секция «Unity CLI»).

Это шаблон проекта, можем вносить любые изменения без поддержки обратной совместимости.

Git: коммиты и push делает пользователь; агент коммитит только по явной просьбе.

### Unity-ассеты

- `.meta`: при удалении или перемещении файла удалить/переместить парный `.meta`. Для новых файлов `.meta` сгенерирует Unity при refresh — руками не создавать.
- `.prefab` / `.unity` / `.asset`: точечные правки сериализованных полей допустимы прямо в YAML; структурные изменения (иерархия, новые компоненты, ссылки между объектами) — выносить в «Ручные шаги пользователя».

## Unity CLI

Полная статья — `unity-my-template-docs/Process/Unity-CLI.md`. Выжимка:

У проекта есть прямой канал в **открытый** Unity Editor: программа `unity` (Unity CLI, вне репозитория, в `PATH`) плюс пакет `com.unity.pipeline` в `Packages/manifest.json`, который поднимает локальный HTTP-API редактора на `127.0.0.1:7800`. Ставить пакет не нужно — он уже в манифесте.

Когда задача упирается в живой редактор — посмотреть консоль после правки, дождаться перекомпиляции, прогнать настоящий Test Runner — агент идёт сюда, а не просит пользователя щёлкать в Unity.

- Первое действие всегда `unity status`: без строки со `state: ready` остальное бессмысленно (редактор закрыт или пакет не поднялся — это ручной шаг пользователя).
- Каталог команд и **схему их параметров** редактор отдаёт сам: `unity list --json` → `.data.tools` (~140 записей). Имена параметров берутся оттуда, а не по памяти.
- Вызов — `unity cmd <tool> --<параметр>=<значение> --json`; результат в `.data.result`. Форма `result` не единообразна: где-то объект, где-то строка с JSON внутри.
- Практический минимум: `recompile_status` (компилируется ли), `get_console_logs --severity=error --limit=50` (ошибки консоли), `run_tests --mode=editor --async_tests=true` + опрос `test_status` (Test Runner; на этом проекте 374 теста за ~4 с).
- **Неизвестный параметр не ошибка**: CLI его передаст, инструмент молча проигнорирует, вызов вернёт `success: true` с дефолтным результатом. Судить об успехе по коду возврата нельзя — сверять результат с ожиданием.
- Разрушающие инструменты требуют `confirm=true` (`delete_asset`, `switch_build_target`, перезапись `write_text_file`, `package_add`/`package_remove`). Обходить эту защиту не нужно: правки в `Assets/` агент делает файловыми инструментами в рамках заявленного скоупа.
- Канал **не заменяет** `Tools/fast-tests.ps1` (тот работает при закрытом редакторе и за секунды), **не отменяет** `@user`-приёмку и **не входит** в Stop-цепочку хуков.

## Документация

Атомарная документация проекта лежит в Obsidian vault `unity-my-template-docs/`. `AGENTS.md` — быстрый роутер и набор обязательных правил, а не полная документация.

Перед изменениями в архитектурных или фичевых зонах агент должен:
1. Найти релевантную статью через `unity-my-template-docs/01-Agent-Navigation.md`.
2. Прочитать секции `Для агента`, `Инварианты`, `Как расширять` / `Как добавить` и `Когда обновлять`.
3. Если код расходится с документацией — считать код источником истины и обновить статью вместе с кодом.

Ключевые входные точки:
- `unity-my-template-docs/Architecture/Naming.md` — конвенция наименования: суффиксы, глоссарий, инварианты.
- `unity-my-template-docs/Architecture/Class-Interaction.md` — как классы узнают друг о друге и передают управление: DI, сигнал против вызова, что видно наружу.
- `unity-my-template-docs/Architecture/Foundation-vs-Features.md` — границы `Foundation` и `Features`.
- `unity-my-template-docs/Architecture/Initialization-LifecycleEntity.md` — lifecycle системных компонентов.
- `unity-my-template-docs/Architecture/UI-Views.md` — окна, popup-ы, `ViewRouter`, `AutoWindow` / `AutoPopup`.
- `unity-my-template-docs/Architecture/UI-MVVM.md` — UI-логика фич: ViewModel, биндинги, правила R3.
- `unity-my-template-docs/Architecture/Assets-Addressables.md` — загрузка ассетов, кэш, время жизни, `IAssetScope`.
- `unity-my-template-docs/Architecture/Ads.md` — реклама: контракт, кулдаун, заглушка редактора, подключение сети.
- `unity-my-template-docs/Architecture/Testing-TDD.md` — тесты и TDD-цикл: что тестируем, фейки, запуск.
- `unity-my-template-docs/Architecture/Logger.md` — каналы, категории, verbosity, `[AutoLogger]`, guard хот-пасса.
- `unity-my-template-docs/Architecture/Audio.md` — фасад звука, ключи клипов, громкость, mute на рекламе.
- `unity-my-template-docs/Architecture/Signals.md` — шина, каталог сигналов шаблона, чистка подписок.
- `unity-my-template-docs/Architecture/Configs.md` — источники, `WarmUp`, политика отказа, добавление конфига.
- `unity-my-template-docs/Architecture/Utilities.md` — `Result<T>`, `EntityStatus`, расширения, что сюда попадает.
- `unity-my-template-docs/Architecture/LiveOps.md` — контракты бэкенда, оффлайн-дефолты, подключение провайдера.
- `unity-my-template-docs/Architecture/Analytics.md` — событие, роутинг по сервисам, где объявлять события фичи.
- `unity-my-template-docs/Architecture/Feature-Items.md` — экономика: `IInventory`, счётчики, ключи валют.
- `unity-my-template-docs/Architecture/Feature-Clicker.md` — эталонная фича: все шесть слоёв MVVM на живом примере.
- `unity-my-template-docs/Architecture/Feature-DailyBonus.md` — `IConditionalEntity`, суточный сброс, streak, префабы дней.
- `unity-my-template-docs/Process/Skills.md` — скиллы `/feature` и `/prompt-engineer`, когда скилл, когда хук.
- `unity-my-template-docs/Process/Unity-CLI.md` — канал в открытый редактор: `unity status` / `list` / `cmd`, консоль, перекомпиляция, Test Runner.
- `unity-my-template-docs/Process/SRDebugger.md` — надгробие: дев-оверлей SRDebugger выпилен как платный плагин. Рантайм-диагностика — логи `ILogChannel` и `unity cmd get_console_logs`.
- `unity-my-template-docs/Recipes/Add-UI-Window.md` — добавление игрового окна или popup-а.
- `unity-my-template-docs/Process/Tickets.md` — конвенция тикетов: frontmatter, скелеты, `related`, WIP-лимит.
- `unity-my-template-docs/Process/Hooks.md` — цепочка Stop-хуков и что каждый закрывает.

Если задача затрагивает `source_paths` из frontmatter статьи, агент должен проверить эти файлы и обновить статью при изменении описанного поведения.

Пропуск ловит Stop-хук `docs-coverage`: по изменениям рабочего дерева он строит обратный индекс `source_paths` → статьи и ищет новые публичные типы в `Foundation/`, не упомянутые ни в одной статье. Это **сигнал к сверке, а не приговор**: машина не знает, задевает ли правка описанное поведение, поэтому агент обязан либо обновить статью, либо явно написать в ответе, почему обновление не требуется. Ручной прогон — `powershell -File Tools/docs-coverage.ps1`. Устройство всей цепочки хуков — `unity-my-template-docs/Process/Hooks.md`.

`AGENTS.md` содержит краткие выжимки из документации: если меняется поведение, описанное здесь, обновить и соответствующую секцию `AGENTS.md`.

## Тикеты / Kanban

Полная конвенция — `unity-my-template-docs/Process/Tickets.md`; при расхождении права у статьи. Выжимка:

Рабочие тикеты агента ведутся отдельными markdown-файлами в `unity-my-template-docs/Tasks/`: `Features/UMT-Feature-N.md`, `Bugs/UMT-Bug-N.md`, `Epics/UMT-Epic-N.md`. Канон, с которого копировать — номер 1 каждого типа; живую задачу туда не писать. `N` — максимальный номер **своего типа** + 1. Текст тикета — на русском. Канонический статус хранится во frontmatter поля `status`: `Todo`, `In Progress`, `Done`, `Cancelled`.

- Тикет обязателен для любых изменений кода, ассетов или документации; анализ, ревью и ответы на вопросы тикета не требуют. Если тикета нет — сначала создать его в нужной подпапке.
- Frontmatter обязателен целиком: `title`, `type: task`, `kind` (`feature` / `bug` / `epic`), `status`, `area`, `module`, `related`, `created`, `updated`, `tags`. Опциональны `epic`, `blocked_by`.
- `area` ∈ {`Foundation`, `Features`, `Project`, `Cross-cutting`}. `module` — значение `module` какой-нибудь статьи vault (`powershell -File Tools/ticket-format.ps1 -ListModules`), склейки через `/` запрещены.
- `related:` **непустой** и ведёт только на статьи vault; на тикеты ссылаются поля `epic:` и `blocked_by:`. `module` тикета обязан совпасть с `module` хотя бы одной статьи из `related:`. Нет подходящей статьи — тикет получает пункт «завести статью X», а не молчаливое исключение.
- `tags` содержит ядро `[task, <kind>, <area-slug>, <module-slug>]`; свободные теги сверх — можно.
- Тело следует скелету по `kind`: feature — Цель / Проблема / **Скоуп** / **Критерии Done**; bug — **Симптом** / Воспроизведение / Причина / **Решение** / **Критерии Done**; epic — **Цель** / **Проблема** / **Подтикеты** / **Критерии Done**. Жирным — то, что проверяет `ticket-format.ps1`; остальное даёт шаблон, но машинно не требуется: пустая секция ради зелёного хука хуже отсутствующей. Секция «Подтикеты» эпика — dataview по `epic:`, ручной чеклист остаётся только для работы, не вынесенной в подтикеты.
- Сгенерированный отчёт в тело тикета не вставляется: данные — файлом в `.agent-state/`, в тикете выводы и ссылка на путь.
- Перед началом активной работы перевести связанный тикет в `In Progress`. Одновременно `In Progress` — **не больше трёх** тикетов `kind: feature` / `kind: bug`; эпики в лимит не входят.
- В `Done` переводить только после того, как пользователь подтвердил компиляцию и результат.
- Если тикет отменён (потерял актуальность, дубликат, оказался не багом) — перевести в `Cancelled`, кратко указав причину в теле тикета.
- Если работа планируется в Plan-mode, полный скоуп фичи фиксируется в тикете: чеклист задач, затронутые зоны, критерии Done и блокирующие вопросы.
- Во время работы поддерживать тикет актуальным, если меняется скоуп или критерии завершения.
- Пункт, который может закрыть **только пользователь** (компиляция в Unity, зелёный Test Runner, прогон сцены, настройка через Inspector, вставка настроек вне репозитория), пишется с маркером `@user` сразу после чекбокса: `- [ ] @user Компиляция и зелёный Test Runner`. Маркер — единственный машинный признак приёмки; без него пункт считается технической работой агента.

Требование закрыто Stop-хуком `ticket-check`, который гоняет два скрипта подряд:

- `Tools/ticket-check.ps1` — изменения вне `unity-my-template-docs/Tasks/` при отсутствии тикета в статусе `In Progress` возвращают ход. Изменения только внутри `Tasks/` проверку не запускают — иначе создание самого тикета требовало бы тикета.
- `Tools/ticket-format.ps1` — формат тикетов: обязательные поля, закрытый список `area`, словарь `module`, сверка `module` ↔ `related`, ядро `tags`, заголовки скелета, резолв `epic:`, WIP-лимит, а также правило `ticket-test-reference`: тест, названный в тикете `status: Done` с `created` от 2026-08-13, обязан существовать в тестовой сборке и быть зелёным в последнем прогоне журнала. Без ключей — тикеты, изменённые относительно `HEAD`; `-All` — полный прогон; есть `-Files`, `-BaseRef`, `-ListModules`. Исключение — строка `<файл>:<правило> # <причина>` в `Tools/ticket-format.exceptions.txt`, причина обязательна.

### Приёмка: когда остались только `@user`-пункты

Как только в тикете незакрытыми остались **только** `@user`-пункты, работа агента закончена, и ход нельзя завершать прозой «осталось подтвердить». Агент спрашивает через `AskUserQuestion` ровно двумя вариантами: «Да, всё в порядке — закрываем тикет» и «Ещё не проверял — вернусь позже». Варианта «Нет» быть не должно: замечания пользователь пишет в штатное свободное поле `Other`, и отдельная кнопка «нет» его дублирует. Два варианта — минимум инструмента (`options: minItems 2`), одним «Да» обойтись нельзя.

- Ответ «да» — проставить `[x]` во всех `@user`-пунктах, `status: Done`, обновить `updated`. Коммит по-прежнему делает пользователь.
- Ответ «ещё не проверял» — не менять ничего, тикет остаётся `In Progress`, ход завершается. Хук уже записал хэш в этом ходу и переспросит только после изменений в рабочем дереве.
- Свободный текст в `Other` — внести замечания в тикет **отдельными пунктами** (без `@user`) и продолжить работу. Пока такой пункт открыт, гейт молчит: тикет снова ждёт приёмки только после его закрытия.

Гейт закрыт Stop-хуком `acceptance-check` — последним в цепочке, потому что спрашивать про приёмку имеет смысл только когда тесты, имена, доки, скоуп и тикет чисты. Проверка идёт по каждому тикету `In Progress` отдельно, но **только по тем, файлы которых правили в этой сессии**: вопрос про тикет, который в чате не открывали, пользователю нечем закрыть. Список сессии ведёт сам хук (`.agent-state/AcceptanceCheck/session-tickets.txt`, ключ — `session_id`); чеклисты внутри блоков кода игнорируются как примеры. Ручной прогон — `powershell -File Tools/acceptance-check.ps1` (без ключей это полный скан `Tasks/`, отладочный режим); механика списка и дедупликации — `unity-my-template-docs/Process/Hooks.md`.

## Workflow и принципы работы

### Когда остановиться и запросить план

Перед написанием кода остановиться, предложить письменный план и ждать явного одобрения, если задача:
- Затрагивает несколько фич или архитектурных слоёв.
- Меняет публичный контракт: интерфейс, событие, формат данных.
- Требует решения с компромиссами: производительность vs читаемость, новая зависимость, изменение жизненного цикла.

Этот список — **исчерпывающий перечень блокирующих случаев** в проекте: вне его действует базовое правило «спрашивать только блокирующее», и для очевидных мелких правок агент работает без формального плана, сохраняя хирургичность изменений.

### Чем подтверждать утверждения

Базовое правило достоверности требует подтверждения в том же ходу. В этом проекте им считается:
- ссылка `file:line` на прочитанный файл;
- прогон `Tools/fast-tests.ps1`, `Tools/naming-check.ps1`, `Tools/docs-coverage.ps1`, `Tools/generator-tests.ps1`, `Tools/mutation-check.ps1`;
- статья из `unity-my-template-docs/` (код при расхождении сильнее статьи).

Компиляция и Test Runner в Unity — подтверждение пользователя, а не агента: без него результат остаётся непроверенным.

### Заявленный скоуп

Состояние всей оснастки живёт в `.agent-state/` в корне (в `.gitignore`): журнал прогонов, снапшот старта сессии, заявка скоупа, метки дедупликации хуков. Не в `Temp/` — её чистит Unity Editor, и состояние гейтов пропадало после запуска редактора. В `Temp/` остаются только артефакты сборки.

До первой правки агент перечисляет файлы, которые тронет, и пишет тот же список в `.agent-state/ScopeCheck/declared.txt` (по строке на путь от корня проекта, допустимы `#`-комментарии и `*`-маски). Stop-хук `Tools/hook-scope-check.ps1` сверяет его с `git diff` и возвращает ход, если тронуто незаявленное или список не заведён вовсе.

Предсуществующее состояние рабочего дерева агенту не предъявляется: `SessionStart`-хук `Tools/hook-scope-baseline.ps1` снимает снапшот грязных путей (`.agent-state/ScopeCheck/baseline.txt`, `<mtime>|<path>`), и путь из снапшота с неизменившимся mtime из сверки выпадает. Поэтому ход без правок при грязном дереве завершается тихо, а «заявить» чужие изменения ради разблокировки не нужно. Нет снапшота — проверка остаётся строгой.

### Проектирование новой фичи

Новая фича проектируется скиллом `/feature`: проблема → разведка read-only → варианты с ценником → тикет, **без написания кода**. Реализация начинается отдельной сессией по готовому тикету — чистый контекст на реализации входит в замысел, а не является формальностью. Разбор скилла по фазам и правило «когда скилл, когда хук» — `unity-my-template-docs/Process/Skills.md`.

Если пользователь ставит задачу сразу решением («сделай X в файле Y»), можно выполнять как есть — но пространство решений при этом закрыто, и архитектурных альтернатив агент не предложит. Когда задача этого стоит, предложить `/feature`.

### Ручные шаги пользователя

Если для запуска или проверки в Unity нужны действия, которые агент не может выполнить сам, перечислить их в конце отчёта отдельным разделом. Примеры: добавить GameObject в сцену, зарегистрировать Addressables, обновить Build Settings, настроить prefab / asset через Inspector, проставить `[SerializeField]`-ссылки, обновить пакет или ключи в `ProjectSettings`.

- Каждый пункт должен быть конкретным: путь к файлу, сцене или окну, имя GameObject / компонента / ключа, последовательность кликов.
- Если ручных шагов нет и достаточно refresh assets, отдельный раздел не добавлять.
- Если есть проверочные действия, приложить короткий чек-лист: какую сцену запустить, что нажать и какой результат увидеть.

## TDD / Тесты

Полная статья — `unity-my-template-docs/Architecture/Testing-TDD.md`. Выжимка:

- **Test-first обязателен** для Model, ViewModel, утилит и Foundation-сервисов с логикой: красный тест → минимальная реализация → рефакторинг. Требование машинное, а не на честном слове: `fast-tests` дописывает журнал прогонов `.agent-state/FastTests/history.jsonl`, а Stop-гейт `tdd-check` требует, чтобы новый тест хотя бы раз упал **на ассерте** до того, как стал зелёным (`test-never-failed`), и чтобы тест, зелёный в `HEAD`, не переписывался вместо реализации (`green-test-rewritten`). Практический вывод: тест и реализацию нельзя писать одним заходом — прогони `fast-tests` на красном тесте. Красный по `NullReferenceException` / `MissingMethodException` фазой red не считается: это отсутствующий код, а не непройденная проверка. Исключения — `Tools/tdd-check.exceptions.txt` (строка `<правило>:<тест> # <причина>`); ручной прогон — `powershell -File Tools/tdd-check.ps1`. Для наследников `SaveBlob` — тесты-контракты (MemoryPack roundtrip в существующий инстанс, допустим test-after). View, префабы, конфиги и `*Core` (composition root) не тестируются — желание протестировать view/Core означает, что логика утекла не в тот слой.
- Тестовые сборки: `Foundation.Tests` (`Assets/Framework/Foundation/Tests/`) и `Features.Tests` (`Assets/Framework/Features/Tests/`), обе EditMode-only. `internal` открыт тестам через `InternalsVisibleTo` (AssemblyInfo.cs обеих сборок).
- Быстрый цикл агента: `powershell -File Tools/fast-tests.ps1` — компиляция + прогон вне Unity за секунды, редактор закрывать не нужно (требует сгенерированных csproj). Финальная истина — Unity Test Runner (пользователь) или `Tools/run-tests.ps1` при закрытом редакторе. Тестовые сборки компилируются **строго по `.asmdef`**: `references` и `precompiledReferences` резолвятся поимённо, из csproj берутся только движковые и рантаймовые сборки, которых в asmdef нет. Недостающая ссылка в тестовом asmdef (`overrideReferences: true`) даёт красный `fast-tests`, а не сюрприз в Test Runner. Объявленная, но не найденная DLL — явная ошибка скрипта с именем сборки и списком мест, где искали.
- Прогон автоматизирован Stop-хуком (`Tools/hook-fast-tests.ps1`, зарегистрирован в `.claude/settings.json`): когда с прошлого прогона менялись `.cs` в `Assets/Framework/`, тесты запускаются в конце хода сами, и красный результат возвращает агента к работе. Ручной вызов `fast-tests.ps1` остаётся для проверки в середине хода. Метка последнего прогона — `.agent-state/FastTests/.last-hook-run`.
- **Мутационное тестирование** — `powershell -File Tools/mutation-check.ps1` (`-SelfTest` — самопроверка мутатора, `-All` — файл целиком, `-Limit` — потолок мутантов). Вносит в изменённый код одну точечную правку поведения (`> ↔ >=`, `< ↔ <=`, `== ↔ !=`, `&& ↔ ||`, `true ↔ false`, `+ ↔ -`, удаление вызова-инструкции, `return x` → `return default`) и смотрит, покраснеют ли тесты. Зелёные на мутанте = **выживший мутант**: строка исполняется, но поведение никем не проверено. Это единственное измерение, не опирающееся на артефакт того же агента, поэтому агент **обязан** прогнать его руками после зелёного цикла на нетривиальной логике: границы и сравнения, ветвления, накопление состояния, новый Foundation-сервис. Отчёт делит операторы на **поведенческие** (границы, равенство, логика, арифметика — верхняя строка отчёта) и **слабые** (удаление вызова, булев литерал, `return default`): у вторых выживший бывает ненаблюдаемым по построению, и в одну цифру они не складываются. Код, который по конвенции не тестируется, не мутируется вовсе: наследники `UnityEngine.Object` и типы с суффиксом `Core` отсеиваются семантикой (`Mutator scan`), тонкие адаптеры к внешним системам — строкой с обязательной причиной в `Tools/mutation-check.exceptions.txt`. В Stop-цепочке сознательно нет — прогон идёт минуты, а выживший мутант требует ручного разбора (эквивалентная мутация против дыры в ассертах); обоснование — `unity-my-template-docs/Process/Hooks.md`, раздел «Почему `mutation-check` не в цепочке». Компиляция общая с `fast-tests` (`Tools/fast-build.ps1`): мутант обязан собираться тем же набором ссылок и дефайнов, что тесты.
- **Property-based инварианты** — `PropertyCheck` (`Assets/Framework/Foundation/Tests/PropertyCheck.cs`, своя реализация, внешних библиотек нет): `ForAll(generate, assert)` для свойств, которые обязаны держаться на **любом** входе («счётчик не уходит в минус», «время не идёт назад», roundtrip сейва). Seed фиксирован и печатается при падении — контрпример обязан воспроизводиться руками; уменьшение контрпримера задаётся делегатом `shrink`. Конкретный сценарий с осмысленными числами остаётся обычным примером: одно не заменяет другое. Такой тест на уже корректный код красным не бывает, поэтому категория проходит через исключение на класс в `Tools/tdd-check.exceptions.txt` — портить рабочий код ради красного прогона нельзя.
- Мок-фреймворков нет и не добавляем: ручные фейки. Общие — `Assets/Framework/Foundation/Tests/Fakes/`, фичевые — `Assets/Framework/Features/Tests/Fakes/`. Конфиги в тестах строятся через Newtonsoft из JSON (`FoundationTestConfigs.cs` / `FeaturesTestConfigs.cs`).
- Стиль: NUnit, naming `Method_ExpectedBehavior_Condition`, Arrange/Act/Assert. `LogAssert` — только для тестов, которым он действительно нужен (вне Unity они помечаются SKIPPED); при инжектируемом `ILogChannel` предпочитать `FakeLogChannel`.
- Тест, требующий сцену, GameObject или VContainer-скоуп, — сигнал чинить дизайн, а не писать PlayMode-тест.
- Рефлексивная регистрация покрыта тестами-инвариантами: `AutoTypeScannerTests` (скан и фильтры сборок) и `RegistrationGraphTests` (каждая `[Inject]`-зависимость зарегистрированного типа кем-то закрыта). Меняешь правила регистрации или добавляешь ручную регистрацию в scope — синхронизируй `RegistrationGraphTests`.
- Генератор тестируется отдельным `dotnet test`-проектом `Tools/AutoDecorators.Generator.Tests/` (snapshot вывода + диагностики `ADG001`–`ADG004`, компиляция против стабов Core в `FrameworkStubs.cs`): `powershell -File Tools/generator-tests.ps1`. Меняешь публичные типы, на которые опирается генератор, — правь и стабы.
- CI: `.github/workflows/generator-tests.yml` гоняет тесты генератора на GitHub-hosted раннере (Unity не нужен). EditMode-тестам нужен self-hosted раннер с Unity; его нет, поэтому `.github/workflows/unity-tests.yml` запускается только вручную.

## Foundation vs Features

- `Assets/Framework/Foundation/` (asmdef `Foundation`) — переиспользуемый foundation-слой: инициализация, DI-расширения, ViewRouter, Signals, Save/Load, Logger, Asset, Localization, Audio-инфраструктура, Ads и т.п. Кода, привязанного к конкретной механике игры, тут быть не должно — в том числе экономики (`Inventory`, `ItemsData`, `CurrenciesConfig`) и готовых игровых MonoBehaviour-ов UI (`CurrencyView`, `RewardRowLayout`): всё это живёт в `Features`.
- `Assets/Framework/Features/` (asmdef `Features`, ссылается на `Foundation`) — фичи игры: окна (`MainMenu`, `Settings`), геймплей (`Clicker`, `DailyBonus`), игровые scope-ы и игровые расширения save-данных.
- `Assets/Framework/Integrations/` — отдельные asmdef-адаптеры: зависят от `Core` и конкретного стороннего пакета; `Core` не ссылается на них и не получает их зависимости.
- Правило: если фича может пригодиться в другом проекте — `Foundation/`. Если она про *этот* проект — `Features/`.
- **Платные плагины.** Ни в `Foundation`, ни в `Features` их нет и заводить не нужно: шаблон едет в другие проекты через subtree, и вторая игра не должна упираться в чужую лицензию. Кнопка «вызвать метод из инспектора» — штатный `[ContextMenu]`, подсказки — `[Header]` / `[Tooltip]`; если штатного не хватает — кастомный `PropertyDrawer` в Editor-сборке.

## Инициализация

Ядро инициализации — `LifecycleEntity` (`Assets/Framework/Foundation/Initialization/Scripts/LifecycleEntity.cs`). Три фазы:

1. `Load` — параллельно (всё внешнее: ассеты view/canvas/audio, сейв, локализация)
2. `Init` — параллельно
3. `PostInit` — последовательно по `InitOrder`

Отдельной фазы под конфиги нет — они грузятся до всех фаз (см. «Конфиги»). Сигналов завершения фаз тоже нет: границы наблюдаются через `SceneLoadingProgressSignal` и `SceneStartedSignal`.

`SceneStarter` (`IAsyncStartable`) сначала параллельно прогревает конфиги и часы (`UniTask.WhenAll(IConfigProvider.WarmUp, IClock.WarmUp)`), затем резолвит `IReadOnlyList<LifecycleEntity>` через `IObjectResolver`, фильтрует по `[LifecycleOrderAttribute(scene, order)]`, сортирует, применяет `LifecycleGate`, декорирует через `TryDecorateEntities()` (`LifecycleDecoratorPipeline` / AutoView) и прогоняет фазы. Инжектить список entity полем нельзя — VContainer создал бы их при `Build()` scope-а, то есть до прогрева.
В каждой параллельной фазе действует глобальный барьер: сначала параллельно завершаются wrapper-ы всех entity, затем параллельно запускаются base entity. Фаза закрыта для всех entity до начала следующей, поэтому значение, вычисленное в `Load` одной entity, доступно в `Init` любой другой. Каждую фазу и каждую entity внутри неё `SceneStarter` замеряет `Stopwatch`-ом и пишет в лог завершения фазы разбивку от самой медленной (`LifecyclePhaseTimings`).

Новый системный компонент:
1. Унаследовать `LifecycleEntity`, переопределить нужные фазы.
2. `[LifecycleOrderAttribute(SceneConstants.Scenes.X, (int)<XSceneInitOrder>.Y)]` на каждую сцену. Enum-ы порядка — в `Assets/Framework/Foundation/Initialization/Scripts/InitOrder/`.
3. Добавить `[AutoRegistration]` для автоматической регистрации или зарегистрировать вручную через `.AsLifecycleEntity()` (см. `VContainerBuilderExtensions.cs`).

Статусы каждого `LifecycleEntity`:

- `IsEnabled`: если entity инжектит конфиг или реализует `IConditionalEntity`, значение выставляет `LifecycleGate` до фаз — не дублировать в `Init`; без источника решения entity считается включённой по умолчанию, поэтому в начале `Init` обязательно вызвать `SetEnabled(true)`.
- `IsInited`: выставляет lifecycle — `LifecycleEntity.InitPhase` вызывает `SetInited(true)` после успешного завершения `Init()`. Руками `SetInited()` в `Init` не вызывать. Если `Init` бросил, статус остаётся `false`. Сознательный ранний выход, при котором инициализация не доведена до конца, вызывает `SetInited(false)` до `return` — такой явный отказ lifecycle не перебивает (покрыто тестом `LifecycleEntityTests`; «попап сегодня не нужен» у `DailyBonusCore` гасится через `IConditionalEntity.ShouldRun()` до фаз, а не через `SetInited(false)` в `Init`).
- `IsActive`: вызвать `SetActive()` только когда entity готова принимать работу и фактически активна; если активация происходит позже `Init`, выставить статус в точке активации, а при деактивации сбросить через `SetActive(false)`.

Классы вне иерархии `LifecycleEntity`, которые держат собственный `EntityStatus` (`AudioController`, `LoadingCurtainController`), фаз не проходят и выставляют все статусы сами.

`[AutoRegistration(Lifetime.X)]` работает и для обычных сервисов (не-`LifecycleEntity` регистрируются `AsSelf` + `AsImplementedInterfaces`); дефолт — `Lifetime.Scoped`, то есть «инстанс на сценовый scope, умирает со сценой». Если такой сервис инжектят Singleton-потребители или он держит переживающий сцену кэш — ему нужен `Lifetime.Singleton`: root-контейнер иначе создаст свой инстанс отдельно от сценового (captive dependency, два состояния под одним интерфейсом). Инвариант закрыт тестом `RegistrationGraphTests.Graph_DoesNotCaptureScopedDependencies_InRootSingletons`. Все конкретные наследники `SaveBlob` регистрируются автоматически без атрибута. Скан выполняет `builder.RegisterAutoTypes()` в `RootScope`; сама логика поиска — `AutoTypeScanner.Scan(assemblies)` (в рантайме сборки берутся из AppDomain, в тестах передаются явно). Шорткаты `RegisterSingleton<T>` / `RegisterScoped<T>` остаются для регистраторов (platform-`#if`) и ручных случаев.

## Scope-ы

`BootstrapScope` и единый `SceneScope` поверх `RootScope` (`Assets/Framework/Foundation/Initialization/Scripts/Scopes/`); scope-ы авто-парентятся к root через `VContainerSettings.RootLifetimeScope`. `RootScope` также регистрирует `TimeProvider` (R3 `ObservableSystem.DefaultTimeProvider`; в плеере это Unity-провайдер) — логика получает время только через инжект. `RootGameScope` — feature-надстройка над foundation root scope (`Assets/Framework/Features/Initialization/Scripts/Scopes/`): сериализованный массив `m_Configs` (`ScriptableObject`-конфиги регистрируются по конкретному типу, новый конфиг — drag-and-drop в Inspector `RootScope.prefab`). Переходы сцен — `SceneStateMachine`, имена — `Framework.Foundation.Scenes.SceneConstants`.

Префабы scope лежат в `Initialization/Content/Scopes/` и называются `<имя константы сцены>Scope` (`BootstrapScope`, `StartScope`, `CoreScope`, `MetaScope`). Сцена, объявленная в `[LifecycleOrderAttribute]`, но без scope, молча не выполняет ни одной фазы — инвариант закрыт тестами `LifecycleSceneScopeTests` (`EveryLifecycleScene_HasScopePrefab` + `EveryLifecycleScene_IsDeclaredInSceneConstants`). Наличие экземпляра scope в самой сцене тестом не проверяется.

`SceneLoader` нерентерабелен: `PrepareSceneLoad` возвращает `false`, пока другой запрос pending/loading. Уже запущенная Addressables-загрузка не отменяется и не заменяется новым запросом. Провал загрузки не глотается: `SceneLoader` триггерит `SceneLoadFailedSignal(sceneName, exception)`, по которому `LoadingCurtainController` снимает шторку — иначе она ждала бы `SceneStartedSignal` новой сцены, который уже не придёт.

Регистрация save-блобов и LiveOps — partial-классы под `#if` (`PLAYER_PREFS_SAVE_ENABLED`, файловое сохранение, PlayFab/GamePush). Новый бэкенд save или LiveOps-провайдер — тот же паттерн. Define-ы переключаются в Project Settings → Player → Scripting Define Symbols (`ProjectSettings/ProjectSettings.asset`) — это ручной шаг пользователя.

## Декораторы (AutoWindow / AutoPopup / AutoLogger)

Декораторы убирают boilerplate в фичевых `LifecycleEntity`. Без них в каждом feature core пришлось бы вручную: грузить ассет → создавать view через фабрику → регистрировать в `ViewRouter` → присваивать поле. С декоратором это делается автоматически по атрибуту на поле.

Тип view задаётся выбором атрибута: `[AutoWindow("key")]` → `ViewKind.Window`, `[AutoPopup("key")]` → `ViewKind.Popup`. Их обрабатывает source generator `AutoDecorators.Generator`: на компиляции он генерит partial-часть класса с реализацией `IAutoViewHost` (типизированные биндинги, без рантайм-рефлексии). Класс с такими полями **обязан быть `partial`** — иначе compile error `ADG001`. Ключ view уникален глобально: один и тот же ключ на двух типах в сборке — compile error `ADG004` (внутри одного класса — `ADG003`), а дубль между `Foundation` и `Features` и отсутствующую запись в Addressables ловит EditMode-тест `AddressableKeyTests` (`Assets/Framework/Features/Tests/`, сверяет ключи с `Assets/AddressableAssetsData/AssetGroups/*.asset`). Пример — `MainMenuCore._view` (`AutoWindow`), `SettingsCore._view` (`AutoPopup`). Внутреннее устройство — `unity-my-template-docs/Architecture/UI-Views.md` и `Architecture/Initialization-LifecycleEntity.md`.

Логгер — `[AutoLogger(<...>Constants.LogName, LogCategory.Feature, StatusLogs = true)]` **на классе** (второй аргумент опционален, дефолт — `LogCategory.System`; `StatusLogs` опционален, дефолт `false`). Рантайм-декоратора нет: генератор эмитит в partial-часть свойство `protected ILogChannel Logger { get; private set; }` (в `sealed`-классе — `private ... set;`) и `[Inject]`-метод, получающий логгер из `ILogChannelFactory`, поэтому атрибут работает в любом классе, который инжектит VContainer (не только `LifecycleEntity` и не только в `Features`); класс тоже обязан быть `partial`. `StatusLogs = true` там же вызывает `EnableStatusLogs(entityType)` — ручной вызов в `Init` не нужен; на классе не-`LifecycleEntity` это compile error `ADG002`. Руками поле логгера не объявлять — использовать `Logger`. Пример — `SettingsCore`, `Inventory`, `CurrencyView`, `SceneStarter`, `SaveLoadService`.

Сеттер `Logger` приватный, но доступен внутри самого типа — поэтому тестовый шов `internal`-конструктором просто присваивает `Logger = logger` и фабрика в тесте не нужна (`SaveLoadService`, `SceneLoader`). Если логгер нужен раньше post-inject (в ctor) или внутри собственного `[Inject]`-метода, `[AutoLogger]` не подходит — см. секцию «Конвенции».

Генератор: исходники — `Tools/AutoDecorators.Generator/` (netstandard2.0, Microsoft.CodeAnalysis.CSharp 4.3.0 — требование Unity), собранная DLL — `Assets/Framework/Analyzers/AutoDecorators.Generator.dll` (label `RoslynAnalyzer`, платформы выключены — как у `MemoryPack.Generator.dll`). После изменения генератора нужны оба шага: `powershell -File Tools/generator-tests.ps1` (тесты генератора, `Tools/AutoDecorators.Generator.Tests/`) и `powershell -File Tools/build-generator.ps1` (собирает Unity-овским Roslyn без .NET SDK и сам копирует DLL в `Assets/Framework/Analyzers/`); при установленном SDK эквивалент сборки — `dotnet build Tools/AutoDecorators.Generator -c Release` + копирование руками. Тесты проверяют исходники, Unity использует собранную DLL — без пересборки правка до редактора не дойдёт.

Забытую пересборку ловит `Assets/Framework/Analyzers/AutoDecorators.Generator.dll.hash` — SHA-256 исходников генератора, который пишет сам `build-generator.ps1` после успешного копирования DLL. Это единственная связь между закоммиченной DLL и кодом: без неё рассинхрон не видят ни компилятор, ни `fast-tests`, ни `generator-tests` (последние гоняют **исходники** и остаются зелёными). Сверку делает Stop-хук `Tools/hook-generator-hash.ps1`, один и тот же рассинхрон блокирует ход один раз (`.agent-state/GeneratorHash/.last-report`); ручной прогон — `powershell -File Tools/generator-hash.ps1 -Check`. Хэш считается по тому же набору файлов, что компилирует сборка (верхний уровень `Tools/AutoDecorators.Generator/`, `.cs`), с нормализацией переводов строк. `.hash` коммитится вместе с DLL.

Гейт выключает сущность целиком: `LifecycleGate` до фаз выставляет `Status.IsEnabled` из двух источников — конъюнкции `IsEnabled` всех инжектируемых конфигов и `IConditionalEntity.ShouldRun()`, если сущность его реализует. Выключенная сущность **не выполняет ни одной фазы** ни сама, ни её обёртки: ассеты не грузятся, view не создаётся, `Init` не вызывается. Собственного gate у `AutoViewEntity` нет и добавлять не нужно — условие всегда живёт на уровне сущности.

`IConditionalEntity.ShouldRun()` — для условий, которые конфигом не выразить (награда уже забрана, ивент кончился, туториал пройден): синхронный, вызывается один раз до фаз, когда конфиги, серверное время, сейв и post-inject-зависимости уже готовы. Побочные эффекты допустимы, но конфиг сильнее условия — при выключенном конфиге `ShouldRun` не вызывается. Если `ShouldRun` вернул `true`, фазы идут как обычно, поэтому ранние выходы и проверки на `null` внутри `Init` не нужны. Пример — `DailyBonusCore`. Не покрывает окна, открываемые позже по действию игрока, и async-условия.

Ручной `SetEnabled(_config.IsEnabled)` в `Init` не нужен. Сущности без гейта функционально включены по умолчанию и явно вызывают `SetEnabled(true)` в `Init` (сам `EntityStatus.IsEnabled` до этого равен `false`).

Открытие/закрытие view фича слушает явной подпиской на R3-стримы `MonoView`: `OnOpen` (до show-анимации), `OnOpened` (после), `OnClose` (до hide-анимации закрытия), `OnClosed` (после) — события без replay; `State` — `ReadOnlyReactiveProperty<ViewState>` с текущим состоянием. `OnClose`/`OnClosed` срабатывают ровно один раз на фактический переход в `Closed`; временный `Suspended` popup-а закрытием не считается, restore из `Suspended` снова триггерит `OnOpen`/`OnOpened`. Подписываться через шорткаты `SubscribeOnOpen/OnOpened/OnClose/OnClosed(Action)` — отписка автоматическая при уничтожении view, `AddTo` не нужен; для «сырых» Observable действует общее правило `.AddTo(...)`. Дубль ключа на двух view-полях — compile error `ADG003`. Пример — `DailyBonusCore`: `_popupView.SubscribeOnClosed(Dispose)`.

## Сигналы

Полная статья — `unity-my-template-docs/Architecture/Signals.md` (устройство шины, каталог всех сигналов шаблона с источниками и подписчиками, как расширять). Выжимка:

- `ReactiveSignalBus` (R3) реализует `ISignalBus`. Стрим идентифицируется **только типом сигнала** — данные несёт сам сигнал (payload-in-signal). Все сигналы реализуют `ISignal`.
- Маркер-сигнал (без данных) — пустой класс: `Trigger<T>()` / `Subscribe<T>(Action)`. Payload-сигнал — данные в readonly-полях: `Trigger(new T(...))` / `Subscribe<T>(Action<T>)`.
- Шина **не делает replay**: `Trigger` без активных подписчиков теряется. Сигнал — уведомление о переходе, а не источник текущего состояния (для состояния — `ReadOnlyReactiveProperty` у владельца).
- Правило чистки (общее для сигналов и любых R3-подписок): **любой `Subscribe()` немедленно заканчивается `.AddTo(...)`**. `MonoBehaviour` — `.AddTo(this)`; остальные классы — `DisposableBag _subscriptions` + `.AddTo(ref _subscriptions)` (bag — struct, только через `ref`), в `Dispose` — `_subscriptions.Dispose()`.
- Имя — прошедшее время, суффикс `Signal`, без префикса `On` (правила `signal-suffix` / `signal-on-prefix` в `naming-check.ps1`). Файлы — в `Foundation/*/Signals/` или рядом с feature-кодом.

## Save / Load

`SaveBlob` (`[MemoryPackable]`, abstract) — база для сериализуемого состояния. Конкретные классы (`ItemsData`, `ClickerData`) регистрируются автоматически (`RegisterAutoTypes` биндит и как `SaveBlob`, и как `T`). `SaveEnvelope` работает через активный `ISaveStorage` (файл или PlayerPrefs — по define).

`ISaveStorage.TryReadAsync()` возвращает `SaveReadResult` со статусом `Empty` / `Success` / `Corrupted`; повреждённый payload всегда проходит через `QuarantineAsync`, а не маскируется под пустой слот.

На `OnApplicationQuit` и на `OnApplicationPause(true)` `ProgressSaver` вызывает синхронный `IDataSaver.SaveDataImmediate()`: процесс не должен умереть раньше записи, а на мобильных kill приходит именно из фона. Ручные триггеры не зависят от автосейв-таймера. После начала immediate-записи storage не выполняет старые отложенные async-записи (и файловый, и PlayerPrefs); каждая запись PlayerPrefs заканчивается `PlayerPrefs.Save()`, иначе payload остаётся только в памяти.

`SaveBlob` хранит только простые сериализуемые значения — `ReactiveProperty` и прочие типы из R3 в сохраняемую схему не попадают (иначе формат сейва зависит от версии пакета). Реактивная обёртка живёт слоем выше: `ItemsData` держит `Dictionary<string, BigInteger>`, `ItemCounter` — `ReactiveProperty<BigInteger>`, пишет в данные и отдаёт наружу `ReadOnlyReactiveProperty<BigInteger>`. Изменения проходят только через `IInventory`; количество `<= 0` для add/remove отклоняется.

MemoryPack работает в дефолтном режиме (не `VersionTolerant`): **новые сериализуемые члены в `SaveBlob`-классы добавлять только в конец объявления**.

Переименование, смена типа, перенос значения — через версию схемы: конверт хранит `ushort version` на каждый тег, класс поднимает `SaveBlob.CurrentVersion` и реализует `SaveBlob.Migrate(ushort fromVersion)`. Старый член остаётся в классе — из него мигрируют.

Удаление или перестановка сериализуемых членов (мигрировать не из чего) — только через **явный сброс блоба**: поднять `CurrentVersion` и `SaveBlob.MinReadableVersion` до неё. Payload старее рубежа не десериализуется, блоб получает `PrepareNewData()` и информационный лог. Без поднятого рубежа MemoryPack бросит на несовпадении числа членов, и блоб сбросится через `LogError`.

Сбой блоба изолирован: `SaveEnvelope` читает каждый блоб в своём `try/catch` (длина payload-а есть в конверте), поэтому сломанная схема одной фичи не уносит остальной прогресс. Карантин целого файла остаётся для сбоя конверта и для payload-а из будущей сборки (`version > CurrentVersion`). Детали и формат файла — `unity-my-template-docs/Architecture/SaveLoad.md`.

## Конфиги

Полная статья — `unity-my-template-docs/Architecture/Configs.md` (регистрация, `WarmUp`, кэш серверных значений, тесты, как расширять). Выжимка:

- Конфиг — конкретный класс `IConfig` с `[ConfigKeyAttribute("Key")]` на самом типе; потребитель инжектит его как обычную зависимость (`[Inject] private readonly ClickerConfig _config;`). Новый конфиг: класс + атрибут + dummy-json по этому ключу в Addressables, больше ничего — ни регистрации, ни фазы.
- Регистрацию делает `builder.RegisterConfigs()` в `RootScope`; `[ConfigKeyAttribute]` на классе, не реализующем `IConfig`, — исключение на старте.
- **Все** конфиги грузятся одним `IConfigProvider.WarmUp()` в начале `SceneStarter.StartAsync` (параллельно, идемпотентно; неуспешная попытка **не** мемоизируется — отмена или ошибка обязаны ретраиться на следующей сцене). Так сделано потому, что `[Inject]` синхронный, а чтение конфига — нет. Обращение к незагруженному конфигу — `InvalidOperationException` с именем типа.
- **Политика отказа.** `ConfigResolver` пробует `server` → `cache` → `dummy` и берёт первый, который **разобрался**, а не первый, где есть ключ; битое значение уходит в `LogError` и уступает следующему. Битому `dummy` отступать некуда: резолвер бросает, `ConfigReader` превращает это в `Result.HasValue = false`, `ConfigProvider` — в `InvalidOperationException`. Та же политика, что у `SaveEnvelope`: сбой одной единицы данных не уносит остальные, а поломка того, что лежит в сборке, падает громко.
- Тумблер `IsEnabled` руками не проверяется — сущность гасит `LifecycleGate` до фаз.

## Ассеты

Полная статья — `unity-my-template-docs/Architecture/Assets-Addressables.md`. Выжимка:

Ассеты грузятся только через `Assets/Framework/Foundation/Asset/`, прямой `Addressables.*` в фичах запрещён. В Foundation прямой вызов допустим только в `AddressableAssetProvider` и `SceneLoader` (загрузка сцен). Реализация провайдера одна — `AddressableAssetProvider`, `Lifetime.Singleton`.

Интерфейсов три, и они делят слои:

- `IAssetScopeFactory` — **единственная ассет-зависимость фичи**: `[Inject] IAssetScopeFactory` → `CreateScope()`;
- `IAssetScope` — владение ключами: `LoadAssetAsync` / `InstantiateAsync` / `ReleaseInstance` / `ReleaseCompletely` и `Dispose`. Ни `persistent`, ни `ReleaseAsset` здесь **нет** — владелец не может вывести ключ из-под собственного релиза;
- `IAssetProvider` — полная поверхность (`persistent`, `ReleaseAsset`), **только для `Foundation`**: инфраструктура, которая должна пережить сцену или отдать ассет наружу (`IconProvider`, `AudioClipLoader`, `CanvasProvider`, `ConfigResolver`).

Отсюда «кто освободит» — два ответа, а не три: в `Features` это всегда `scope.Dispose()`, в `Foundation` — либо `persistent: true` с явным `ReleaseCompletely`, либо дефолтная шторка загрузки (`LoadingCurtainShownSignal` → `ReleaseAllNonPersistent`).

Scope трекает только то, что загружено **через него**, и в `Dispose` отпускает каждый ключ **от своего имени**. Владельцев у ключа несколько: корневой (все прямые вызовы `IAssetProvider`) и по одному на каждый `AssetScope`. Поэтому `ReleaseCompletely` не безусловен — он уничтожает инстансы **только этого владельца**, снимает **только его** заявку на persistent и освобождает handle, лишь когда ключ не держит никто и нет живых инстансов. Шторка = корневой владелец отпускает всё, что не заявлено persistent; ключ живого scope её переживает. Вложенные scope-ы независимы: dispose внешнего не трогает ключи вложенного. Примеры — `DailyBonusCore`, `AutoViewEntity`.

Инстанс принадлежит тому, кто его создал, поэтому `IViewFactory.CreateView` требует `IAssetScope owner` без умолчания: иначе ключ префаба принадлежал бы scope-у сущности, а GameObject — корневому владельцу, и `Dispose` сущности оставил бы окно висеть.

`ReleaseInstance` уничтожает инстанс, но оставляет ассет в кэше; `ReleaseAsset` не трогает persistent-ключи и ключи с живыми инстансами. Загрузка, которую к моменту завершения никто не ждёт (все токены отменены), не кэшируется — handle освобождается сразу. Для полей с `[AutoWindow]` / `[AutoPopup]` загрузку и релиз делает декоратор своим scope-ом — руками не трогать.

## Время

Полная статья — `unity-my-template-docs/Architecture/Time.md`. Выжимка:

Время читается **синхронно** через `IClock` (`Assets/Framework/Foundation/Time/`). Никаких `await` ради времени: серверное время синхронизируется один раз в `SceneStarter.StartAsync` (`WarmUp` параллельно с `IConfigProvider.WarmUp`, идемпотентен), дальше часы идут по монотонному `IRealtimeSource`. Асинхронность осталась только в `IServerTimeSource.TryFetchUtc` — источнике синхронизации.

Два источника времени, выбирать осознанно:

- `ServerUtcNow` — награды, ивенты, кулдауны: синхронизировано и монотонно. Подделать его игрок не может только при `Trust == ClockTrust.ServerVerified`; на `LocalFallback` anchor взят с локальных часов и такой гарантии нет.
- `ServerLocalNow` — сброс в местную полночь: серверное время в таймзоне игрока, день честный. Так считает день `DailyBonus`, поэтому и `DailyBonusData.LastRewardDate` хранится в местном времени. Сдвиг в местную зону — деталь `Clock`, публичного `ToDeviceTimeZone` нет: рядом с ним компилировался бы `ToDeviceTimeZone(DateTime.Now)` — двойной сдвиг. Часов устройства в контракте нет вовсе: недоверенное время рядом с доверенным — приглашение взять не то.

Для UI — `ServerNow` (`ReadOnlyReactiveProperty<DateTime>`, тик раз в секунду) и `Countdown(deadlineUtc)` (убывающий `TimeSpan`, завершается ровно на `TimeSpan.Zero`).

- `DateTime.UtcNow` / `DateTime.Now` в игровой логике запрещены — только `IClock` (исключения: сам `Clock` и `LocalServerTimeSource`).
- Ход часов — `IRealtimeSource` (`Stopwatch`), не системные часы: иначе перевод времени игроком ломает и читит таймеры. Инжектируемый `TimeProvider` для этого не подходит (в плеере это `UnityTimeProvider.Update` с `TimeKind.Time`: зависит от `timeScale`, стоит на паузе) — он остаётся источником интервалов для `ServerNow` и `Countdown`.
- `Trust` (`ClockTrust`) говорит, синхронизированы часы (`ServerVerified`) или идут от локального времени (`LocalFallback`). Геттеры времени не бросают: до `WarmUp` и при недоступном сервере часы работают с `LocalFallback`.
- `WarmUp` помечает себя выполненным только после успешного прохода: отменённая синхронизация обязана ретраиться на следующей сцене, иначе `Trust` навсегда останется `LocalFallback`.
- Ресинхронизация — по `ApplicationPauseChangedSignal(false)`: background может заморозить процесс, монотонный тик отстанет.

## Локализация

Полная статья — `unity-my-template-docs/Architecture/Localization.md`. Выжимка:

Стартовый язык выбирается один раз, в `Init` `LocalizationController`-а на Bootstrap-сцене. Проектного кода тут одна обязанность — «откуда взялся код языка» (`ILocaleSource.TryGetLocaleCode()`). Сопоставление кода с локалью руками **не пишется**: `LocalizationSettings.AvailableLocales.GetLocale(code)` уже делает регистронезависимое сравнение, фолбэк по цепочке `CultureInfo.Parent` (`ru-RU` → `ru`, `zh-Hans-CN` → `zh-Hans`) и отсев `PseudoLocale`.

- Контроллер инжектит `IReadOnlyList<ILocaleSource>` и берёт первый источник, который дал язык. Коллекция законно пуста (`ILocaleSource` внесён в `_optionalCollectionElements` в `RegistrationGraphTests`) — тогда локаль не трогается и выбор остаётся за Locale Selectors пакета.
- Источнику, которому нужен внешний SDK, ждать его в момент чтения языка **нельзя**: ожидание живёт в фазе `Load` отдельной `LifecycleEntity` (`YandexSdkEntity`), а барьер между `Load` и `Init` делает чтение синхронным и без гонки. Порядок в `*SceneInitOrder` здесь ничего не гарантирует — он влияет только на `PostInit`.
- `LocalizationSettings` читается только после того, как источник вернул язык: статика пакета недоступна вне Unity, безусловное обращение ломает тесты контроллера.
- **Платформенные плагины без asmdef.** `PluginYourGames` не имеет ни одного `.asmdef`, значит `YG2` живёт в `Assembly-CSharp`, на которую asmdef-сборка сослаться не может. Адаптеры к таким плагинам кладутся в `Assets/Scripts/<Платформа>/` и остаются тонкими: они не тестируются `fast-tests`, поэтому ветвлений сложнее проверки готовности SDK в них быть не должно. Регистрация всё равно автоматическая — `Assembly-CSharp` ссылается на `Foundation`, и `AutoTypeScanner` её сканирует. Давать asmdef самому плагину не нужно: `partial class YG2` размазан по 16 файлам, а 40 Editor-файлов в 19 папках не обёрнуты `#if UNITY_EDITOR`.

## Реклама

Полная статья — `unity-my-template-docs/Architecture/Ads.md`. Выжимка:

Фича видит только фасад `IAdsController` (`Assets/Framework/Foundation/Ads/`) с форматами `Banner` / `Interstitial` / `Rewarded`; прямые вызовы SDK рекламы запрещены. Исход показа всегда один — `AdResult` (`Success` / `Skipped` / `NotReady` / `Failed`); ни один метод не бросает, недоступность выражается через `NotReady`.

- `ShowAsync` — единственный путь исполнения, `Show(...)` внутри делает `ShowAsync(...).Forget()` и разбирает результат. Исключение провайдера превращается в `Failed` и пишется в `LogError`.
- `IsReady(format)` — «готово прямо сейчас»: конфиг формата, рантайм-флаг `SetFormatEnabled`, `provider.IsReady`, истёкший кулдаун и отсутствие активного показа. Пересчёт — по тику `IClock.ServerNow`.
- Ad-сессия (`Interstitial` / `Rewarded`): контроллер поднимает `IsAdPlaying`, зовёт `IAudioController.SetMuted` и триггерит `AdStartedSignal` / `AdFinishedSignal`. `Time.timeScale = 0` не ставится — пауза геймплея делается потребителем через `IsAdPlaying`. Баннер сессией не считается: он идёт через `provider.SetBannerVisible`.
- Кулдаун только у interstitial (`AdsPolicy`, время параметром) и складывается из двух таймеров — от предыдущего показа (`interstitial_cooldown_seconds`) и от старта сессии (`interstitial_session_start_cooldown_seconds`); дедлайн — позднейший из двух. Успешный rewarded перезапускает его при `rewarded_resets_interstitial_cooldown`. Время последнего показа — рантайм-состояние политики, в сейве (`AdsData`) только счётчики, и растут они лишь на `Success`.
- `is_enabled: false` в `AdsConfig` гасит фичу целиком через `LifecycleGate` — контроллер существует, но все вызовы возвращают `NotReady`.
- Реализация сети — одна активная: `IAdsProvider` в отдельном asmdef `Integrations/` + partial-метод `AdsScopeRegistrator.RegisterPlatform` под define. Иначе `EditorAdsProvider` (попап-заглушка с Success/Fail) в редакторе и `NullAdsProvider` в билде.
- Вторая форма — для плагина без asmdef (YG2): провайдер лежит в `Assets/Scripts/<Платформа>/` и регистрируется сам через `[AutoRegistration]`, а `RegisterPlatform` только выставляет `registered = true`. Пример — `YandexAdsProvider` (interstitial + rewarded на YG2, в редакторе не регистрируется).
- Хост попапа-заглушки (`AdsStubPopupHost`) — **Scoped**: `[AutoPopup]`-обёртка живёт сценой. Сам `AdsController` — Singleton и поэтому не имеет права инжектить Scoped `IViewRouter` / `IViewFactory`.

## UI / ViewRouter

`ViewKind`:
- `Window` — окно на весь экран
- `Popup` — всплывающее окно поверх окна

Пост-обработка созданного view — через `IViewSetupStep`: `ViewFactory` прогоняет каждый view
через зарегистрированные шаги, сама оставаясь feature-agnostic. Фича-специфичный до-инжект
регистрирует свой шаг (пример — `CurrencyViewSetupStep` в `Features/Items`).

Как добавить новое UI-окно (паттерн `MainMenu` / `SettingsPopup`):
1. Папка `Assets/Framework/Features/<FeatureName>/`: `Content/` (префаб) + `Scripts/`.
2. Префаб с компонентом-наследником `MonoView<TViewModel>`, лежит там, откуда его грузит `IAssetProvider`.
3. `<FeatureName>Constants.Prefabs.<Window|Popup>` — строковый ключ префаба.
4. Красные тесты на Model/VM в `Assets/Framework/Features/Tests/` (секция «TDD / Тесты»), затем `<FeatureName>ViewModel` в `Scripts/ViewModel/` + биндинги во view (секция «UI-логика (MVVM)») до зелёных тестов.
5. `partial <FeatureName>Core : LifecycleEntity` с `[LifecycleOrderAttribute]` + полем (partial обязателен для генератора):
   ```csharp
   [AutoWindow(<...>Constants.Prefabs.Window)] // или [AutoPopup(...)] для popup-а
   private <FeatureName>View _view;
   ```
   В `Init`: создать VM, `_view.Bind(_viewModel)`; в `Dispose`: `_viewModel?.Dispose()`.
6. Добавить `[AutoRegistration]`; ручную `.AsLifecycleEntity()`-регистрацию использовать только если авторегистрация не подходит.

## UI-логика (MVVM)

Обязательный паттерн для всех окон и popup-ов; полная версия с примерами — `unity-my-template-docs/Architecture/UI-MVVM.md`. База — `Assets/Framework/Foundation/UI/Mvvm/`: `ViewModel` (IDisposable + `protected DisposableBag Subscriptions`), `MonoView<TViewModel>` (окна/popup-ы, `Bind`/`OnBind`), `BindableView<TViewModel>` (дочерние элементы, не окна).

Слои: View (пассивный MonoBehaviour) → ViewModel (чистый C#) → Model (домен) → SaveBlob/Config. Core (`LifecycleEntity`) — composition root: создаёт Model/VM через `new`, зовёт `_view.Bind(vm)`, диспозит VM.

Жёсткие правила:
- Наружу из Model/VM — только `ReadOnlyReactiveProperty<T>` / `Observable<T>` / `ReactiveCommand`; `ReactiveProperty` / `Subject` всегда `private`.
- **Любой `Subscribe()` немедленно заканчивается `.AddTo(...)`** — без исключений. Во view и любом MonoBehaviour — `.AddTo(this)`; в VM — `.AddTo(ref Subscriptions)`; в прочих классах — `.AddTo(ref _subscriptions)` (bag — struct, только через `ref`). VM цепляет в bag и свои команды/модель: `command.AddTo(ref Subscriptions)`, `model.AddTo(ref Subscriptions)`.
- `Bind` у view вызывается один раз за жизнь инстанса — подписки `.AddTo(this)` живут до `Destroy`, повторный `Bind` их задублирует.
- Дискретные действия — `ReactiveCommand`; async/неповторяемые обработчики — `SubscribeAwait(..., AwaitOperation.Drop)` внутри VM; one-shot (переход сцены) — `command.Take(1)`.
- Непрерывный ввод (слайдер) — обычный метод VM; two-way биндинг: сначала `vm.X.Subscribe(v => slider.SetValueWithoutNotify(v))`, потом `slider.OnValueChangedAsObservable().Subscribe(vm.SetX)` — порядок обязателен.
- Домен и вычисления — обычный C#; R3 только для уведомлений/композиции. `Observable.EveryUpdate()` вместо `Update()` запрещён; таймеры — с явным провайдером: в DI-классах инжектируемый `TimeProvider` (зарегистрирован в `RootScope`), во view-коде — `UnityTimeProvider` / `UnityFrameProvider`.
- View не трогает Model/Data напрямую; VM не знает о view. Закрытие собственного view кнопкой — единственная «логика» view (`Close()`).
- Роль называется только `ViewModel` (папка `Scripts/ViewModel/`); имя `Presenter` не используется, а `Controller` в шаблоне значит «фасад подсистемы» и на UI-слое не появляется (секция «Наименование»). Интерфейсы `I*` для фиче-внутренних Model/VM не заводятся — только на границах фич (`ISettingsCore`).
- Базовый класс VM указывать полным именем `Framework.Foundation.UI.Mvvm.ViewModel` (сегмент namespace `ViewModel` затеняет короткое имя).

## Наименование

Полная конвенция — `unity-my-template-docs/Architecture/Naming.md`. Выжимка:

**Три закона.** Имя = роль в системе, не базовый класс и не паттерн. Одно понятие — одно слово во всём шаблоне. Имя должно быть угадываемым: зная роль и домен, тип обязан называться так, чтобы не пришлось открывать папку.

**Суффикс — точное существительное роли, любое.** Закрытого списка разрешённых суффиксов нет: `ItemCounter`, `SaveEnvelope`, `ConfigResolver`, `FrameRateLimiter` законны без внесения куда-либо. Закрыты только два списка — запрещённых и зарезервированных.

- Запрещены всегда: `Manager`, `Helper`, `Utils`, `Handler`.
- Зарезервированы (смысл зафиксирован): `Model`, `ViewModel`, `View`, `Core`, `Controller`, `Service`, `Provider`, `Factory`, `Registry`, `Storage`, `Reader`/`Writer`, `Loader`, `Scope`, `Signal`, `Config`, `Settings`, `Constants`, `Attribute`, `Tests`, префикс `Fake`, суффикс `Base`.
- `Controller` = **фасад подсистемы** (`AdsController`, `AudioController`), и это единственный его смысл — в том числе на UI-слое. `Service` = **тонкий адаптер к внешней системе** (`IAnalyticsService`, `FileService`). `Core` = **только** composition root фичи.
- Условно разрешены в одном узком смысле: `Info` (immutable-снимок), `Data` (конкретный сейв-блоб), `Wrapper`, `Spawner`.

**Жёсткие правила.**

- Атрибут объявляется с суффиксом `Attribute` (на месте применения C# его опускает).
- Сигнал — прошедшее время, без `On`, суффикс `Signal` обязателен: `AdStartedSignal`, `SceneChangeRequestedSignal`.
- `I` — только на интерфейсе; абстрактная база — суффикс `Base`. Пустой интерфейс допустим, только если пустота и есть контракт (generic-constraint, ключ DI, граница фичи поверх другого интерфейса); маркер «просто чтобы пометить» — атрибут.
- namespace = путь от `Assets/` минус служебные сегменты (`Scripts`, `Content`, `Editor` — список закрыт); namespace никогда не совпадает с именем типа внутри него.
- Один публичный тип = один файл, имя файла = имя типа.
- `ScriptableObject` называется `*Settings`; `*Config` зарезервирован за remote-конфигом фичи.
- Аббревиатуры: 2 буквы капсом (`UI`, `IO`), 3+ — PascalCase (`Csv`, `Json`, `Api`).

**Ассеты.** Конвенция покрывает и имена вне `.cs` — полная секция «Имена ассетов» в `Naming.md`.

- Ключ Addressables = имя файла ассета, PascalCase (`ClickerWindow`, `CoreSceneMusic`). Ключ, собираемый форматом из данных, наследует стиль данных (`{0}_icon_atlas` → `gem_icon_atlas`) — это правило, а не исключение: item id — ключ словаря в `ItemsData`, смена регистра стоит миграцию сейва.
- Имя файла `ScriptableObject`-ассета = имя типа. Имя префаба = роль; совпадение с именем компонента не требуется, варианты одного типа легальны (`DailyBonusToday` / `DailyBonusNextDay`).
- Folder-entry в Addressables запрещён: адресуется конкретный ассет. Папка ассетов фичи — `Content/`, не по типу Unity-объектов внутри.
- Вне конвенции: сторонние киты (`Features/UI/Sprites/DefaultUI/**`, `Features/UI/Prefabs/Particles/**`, `Popup0*`) и ассеты, порождённые пакетами (Localization, TMP) — имя задаёт пакет.
- Переименование **файла** ассета безопасно всегда (ссылки идут по GUID) — `git mv` вместе с `.meta`. Переименование **адреса** ломает строковые константы и делается только парой с правкой кода.

**Проверка.** Инварианты машинные: `powershell -File Tools/naming-check.ps1 -All` (без параметров — только изменения относительно `HEAD`). Прогон автоматизирован Stop-хуком `Tools/hook-naming-check.ps1` (после `fast-tests`, до `docs-coverage`); один и тот же набор находок блокирует ход один раз (`.agent-state/NamingCheck/.last-report`). Сознательное исключение — строка в `Tools/naming-check.exceptions.txt` **с причиной**; строка без причины считается ошибкой скрипта, а не разрешением.

Три правила про ассеты (`addressable-address-mismatch`, `addressable-folder-entry`, `scriptableobject-file-type-mismatch`) работают через индекс `guid → путь` по всем `.meta` и потому включаются только при `-All` либо когда в изменениях есть `.asset` / `.meta` под `Assets/`. Связь «константа-ключ → запись в Addressables» проверяет EditMode-тест `AddressableKeyTests` (`Assets/Framework/Features/Tests/`): новый вложенный класс-держатель ключей нужно добавить в его `KeyHolderNames`, иначе он выпадет из проверки.

## Взаимодействие классов

Полная конвенция — `unity-my-template-docs/Architecture/Class-Interaction.md`; при расхождении права у статьи. Выжимка:

**Как A узнаёт о B.**

- Единственный способ получить зависимость — DI по интерфейсу (`[Inject] private readonly IInventory _inventory;`). Статик-синглтон, `Find*ByType`, `GameObject.Find`, `IObjectResolver.Resolve` — только в composition root (тип, унаследованный от `LifetimeScope`). `IObjectResolver.Inject(child)` локатором не считается: это достройка уже созданного объекта.
- Знание односторонне: если A знает B, B не знает об A ничего. Двунаправленное знание лечится инверсией (B отдаёт свойство/стрим) или выносом общего в третий тип; «взаимные интерфейсы» — тот же цикл, записанный незаметно.
- Фича видит только границу другой фичи: `I*`-интерфейс или `*Constants`. Конкретные `SettingsCore` / `SettingsModel` / `SettingsData` для чужой фичи не существуют. Общие подсистемы (`Items`, `UI`, `SaveLoad`) от правила освобождены.

**Как A передаёт управление B** — проверять по порядку, побеждает первый сработавший:

| Что нужно | Механизм |
| --- | --- |
| Результат, исход или гарантия «это произошло» | вызов метода |
| Текущее значение | `ReadOnlyReactiveProperty<T>` |
| Факт перехода, и источник не вправе знать получателя | сигнал |

Эталон — `AdsController.ShowFullscreen`: `_audioController.SetMuted(true)` (`AdsController.cs:160`) и `Trigger(new AdStartedSignal(...))` (`:162`) стоят рядом. Mute обязателен, и реклама за него отвечает — вызов. Пауза геймплея живёт в игровом коде, которого `Foundation` не знает — сигнал. Сигнал не сообщает источнику ни об ошибке подписчика, ни об их отсутствии, поэтому обязательное следствие сигналом не делают.

- Вызов идёт вниз, уведомление — вверх. Нижний слой не вызывает верхний ни методом, ни колбэком, ни интерфейсом-слушателем (`IAdsListener` списком — это `SignalBus`, написанный заново и хуже).
- Колбэк (`Action` в параметре) — только продолжение операции, которую запустил тот же класс. Долгоживущая подписка — стрим или сигнал.
- Сигнал заводится под живого подписчика. Сигнал «на будущее» — контракт, который читают и поддерживают; если это объявленная граница расширения, причина пишется в файл исключений.

**Что B показывает наружу.**

- Только неизменяемое: `ReadOnlyReactiveProperty<T>`, `Observable<T>`, `ReactiveCommand`, `IReadOnlyList` / `IReadOnlyDictionary`, immutable-структуры. `ReactiveProperty`, `Subject`, `List`, `Dictionary`, `HashSet`, массивы, публичные поля и `public event` — private. `private set` не помогает: он защищает ссылку, а не содержимое.
- У изменяемых данных ровно один писатель, остальные читают через его API (`ItemsData` меняет только `Inventory`).
- Метод либо меняет состояние, либо читает. Ожидаемый отказ — `Result<T>` или enum-исход (`AdResult`), не исключение и не `null`.
- Кто создал — тот и диспозит; полученное через DI не диспозится.

Восемь правил проверяются машинно: `powershell -File Tools/interaction-check.ps1 -All` (без ключей — изменения относительно `HEAD`; `-Files`, `-BaseRef`, `-SourceRoot`). Прогон автоматизирован Stop-хуком `interaction-check` (третьим в цепочке, после `naming-check`), один и тот же набор находок блокирует ход один раз. Исключение — строка `<правило>:<токен> # <причина>` в `Tools/interaction-check.exceptions.txt`; временное исключение обязано ссылаться на тикет. Не проверяются машинно и требуют ревью: односторонность знания, направление вызова, колбэк вместо подписки, команда/запрос, единственный писатель, владение.

## Конвенции

- Namespace = путь от `Assets/`. Служебные сегменты `Scripts` / `Content` можно опускать (`Framework.Foundation.UI.Views`). Существующие namespace с суффиксом `.Scripts` остаются как есть — массовое переименование не требуется, правило действует для нового кода.
- DI через `[Inject] private readonly` поля, не через конструкторы (для `MonoBehaviour`/`LifecycleEntity`). Пост-инжект — `[Inject] private void Init()`. Это **VContainer** post-inject; **не путать** с фазой `Init` у `LifecycleEntity` — у них совпало имя, но это разные вещи.
- **Пара ctor-ов «`[Inject]` + шов»** — единственный способ подставить зависимости в тесте, когда DI идёт через поля. Публичный пустой ctor **обязан** нести `[Inject]`, рядом — `internal` ctor с параметрами для тестов:
  ```csharp
  [Inject]
  public SaveLoadService() { }

  // Тестовый шов: в проде поля и Logger заполняет VContainer.
  internal SaveLoadService(ISaveEnvelope saveEnvelope, ISaveStorage storage, ILogChannel logger) { ... }
  ```
  Атрибут обязателен потому, что `VContainer.Internal.TypeAnalyzer` сканирует и `NonPublic` и без явной пометки берёт конструктор с **наибольшим числом параметров** — то есть сам шов; падает это только в рантайме Unity. Инвариант закрыт правилом `injectable-ctor-missing-attribute` в `Tools/naming-check.ps1`. Примеры — `SaveLoadService`, `SaveEnvelope`, `ConfigReader`, `SceneLoader`, `AdsController`, `AnalyticsController`.
- Предпочитать композицию наследованию: когда нет сильной причины для новой иерархии, использовать интерфейс и делегирование.
- Логи — только `ILogChannel` из `Framework.Foundation.Logger`; `Debug.Log*` напрямую запрещён. Дефолт получения канала — `[AutoLogger("Name")]` на `partial`-классе и обращение через `Logger`; если логгер нужен в ctor или в собственном `[Inject]`-методе — `ILogChannelFactory` (двух `[Inject]`-методов на типе быть не должно). Хот-пасс — под guard `if (!Logger.AreLogsEnabled) return;` с форматированием **внутри** guard-а; `LogError` под guard не заворачивать. Полная статья — `unity-my-template-docs/Architecture/Logger.md` (категории, verbosity, статус-логи `EntityStatus`, закрытый список легальных `new LogChannel`).
- Async — **только UniTask**. Код пишется кратко и без переусложнений: никаких лишних `try/catch`, `ConfigureAwait`, обёрток-фабрик и `UniTask.RunOnThreadPool` без причины.
- LINQ — предпочитать **ZLinq**: `.AsValueEnumerable()` перед операторами. В горячих/per-frame путях — обязательно; в one-shot коде инициализации допустим обычный `System.Linq`, если так читается яснее.

## Стиль кода

- Комментариев «что делает код» не пишем — хорошие имена сами объясняют. Допустим только «почему»: неочевидная логика, скрытые инварианты, обход бага.
- Именование: PascalCase для типов/методов/публичных свойств, `_camelCase` для private-полей, `camelCase` для локальных и параметров. Поля с `[SerializeField]` — `m_PascalCase` (конвенция Unity Inspector); private non-serialized — `_camelCase`.
- Переиспользовать уже принятые имена: `Init`, `RefreshTexts`, `_data`, `_config`.
- Не плодить синонимы: если в проекте уже есть `Init`, не вводить рядом `Bind`, `Ensure*`, `Push*` или `Setup*` для той же роли.
- В классе-владельце опускать префикс типа: внутри `ShopController` поле `_data`, а не `_shopData`.
- `var` — везде, где тип очевиден из правой части; явный тип — только если без него читается хуже.

### Null-проверки

Защитные `null`-проверки «на всякий случай» не добавлять. Если поле или параметр получены через DI, инжектятся фреймворком или лежат в обязательной сериализованной ссылке префаба, они должны существовать; `if (x == null) return;` маскирует ошибку вместо того, чтобы выявить её.

Проверять `null` нужно только когда отсутствие объекта является валидным бизнес-сценарием. В таких случаях использовать `Result<T>` из `Framework.Foundation.Utilities`:

```csharp
public Result<ItemInfo> TryGetItem(string id)
{
    return _data.Items.TryGetValue(id, out var item)
        ? Result<ItemInfo>.Success(item)
        : Result<ItemInfo>.Failure();
}

if (_shop.TryGetItem("gem_pack_1").TryGet(out var item))
{
    Use(item);
}
```

Полный контракт `Result<T>` (`TryGet`, `GetValueOrDefault`), `EntityStatus`, расширения и правило «что попадает в `Utilities`» — `unity-my-template-docs/Architecture/Utilities.md`. Границы бэкенда (`IServerConnectionService`, `IRemoteConfigSource`, `IServerTimeSource`, оффлайн-дефолты, подключение провайдера под define) — `unity-my-template-docs/Architecture/LiveOps.md`.
