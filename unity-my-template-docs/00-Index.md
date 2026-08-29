---
title: Project Documentation Index
type: index
area: Project
module: Documentation
status: actual
source_paths:
  - CLAUDE.md
  - README.md
related:
  - "[[01-Agent-Navigation]]"
  - "[[Naming]]"
  - "[[Class-Interaction]]"
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[UI-Views]]"
  - "[[UI-MVVM]]"
  - "[[SaveLoad]]"
  - "[[Time]]"
  - "[[Assets-Addressables]]"
  - "[[Ads]]"
  - "[[Testing-TDD]]"
  - "[[Add-UI-Window]]"
  - "[[Tickets]]"
  - "[[Hooks]]"
  - "[[Unity-CLI]]"
tags:
  - docs
  - index
  - ai-entrypoint
updated: 2026-08-29
---

# Project Documentation Index

## Для агента

Начинай отсюда, если задача требует понять архитектуру проекта перед изменениями. Для быстрого роутинга открой [[01-Agent-Navigation]], затем релевантную статью из `Architecture/`, `Recipes/` или `Process/`.

Если документация расходится с кодом, код считается источником истины. В таком случае обнови соответствующую статью вместе с изменением кода.

Часть статей помечена `status: stub`: у них есть frontmatter, `source_paths` и короткая секция «Для агента», но полного описания подсистемы нет. Заглушка — законная цель для `related:` тикета, но не источник истины.

## Основные входные точки

- `README.md` (корень репозитория) — человеческий вход: стек, слои, запуск тестов, роутер сюда.
- [[01-Agent-Navigation]] — быстрый выбор статьи под задачу.
- [[Naming]] — конвенция наименования: суффиксы, глоссарий, инварианты.
- [[Class-Interaction]] — как классы узнают друг о друге и передают управление: DI, сигнал против вызова, что видно наружу.
- [[Foundation-vs-Features]] — границы между переиспользуемым foundation-слоем и игровыми фичами.
- [[Initialization-LifecycleEntity]] — lifecycle системных компонентов и порядок фаз.
- [[UI-Views]] — окна, popup-ы, canvas-ы и регистрация view.
- [[UI-MVVM]] — UI-логика фич: ViewModel, биндинги, правила R3.
- [[SaveLoad]] — save-data, MemoryPack, формат сейва и правила эволюции схемы.
- [[Time]] — источники времени, синхронизация, обратный отсчёт и суточный сброс.
- [[Assets-Addressables]] — загрузка ассетов, кэш, время жизни и `IAssetScope`.
- [[Ads]] — контракт рекламы, кулдаун, заглушка редактора и подключение сети.
- [[Testing-TDD]] — тесты и TDD-цикл: что тестируем, фейки, запуск.
- [[Add-UI-Window]] — рецепт добавления нового игрового окна.
- [[Tickets]] — конвенция тикетов: frontmatter, скелеты тела, `related`, WIP-лимит.
- [[Hooks]] — цепочка Stop-хуков и что каждый закрывает.
- [[SRDebugger]] — надгробие: дев-оверлей выпилен как платный плагин.
- [[Unity-CLI]] — канал в открытый редактор: консоль, перекомпиляция, настоящий Test Runner.
- `Tasks/Features/` — feature-тикеты агента в формате `UMT-Feature-N`.
- `Tasks/Bugs/` — bug-тикеты агента в формате `UMT-Bug-N`.
- `Tasks/Epics/` — эпики в формате `UMT-Epic-N`.

## Architecture

- [[Naming]]
- [[Class-Interaction]]
- [[Foundation-vs-Features]]
- [[Initialization-LifecycleEntity]]
- [[UI-Views]]
- [[UI-MVVM]]
- [[SaveLoad]]
- [[Time]]
- [[Assets-Addressables]]
- [[Ads]]
- [[Testing-TDD]]
- [[Logger]]
- [[Audio]]
- [[Configs]]
- [[Signals]]
- [[Utilities]]
- [[LiveOps]]
- [[Analytics]]
- [[Feature-Items]]
- [[Feature-Clicker]]
- [[Feature-DailyBonus]]

## Recipes

- [[Add-UI-Window]]

## Process

- [[Tickets]]
- [[Hooks]]
- [[Skills]]
- [[SRDebugger]]
- [[Unity-CLI]]

## Tasks

Тикеты агента лежат в `Tasks/Features/`, `Tasks/Bugs/` и `Tasks/Epics/`. Каждый тикет — отдельный markdown-файл на русском языке; статус задачи хранится во frontmatter поля `status`.

Feature-тикеты называются `UMT-Feature-N.md`, bug-тикеты — `UMT-Bug-N.md`, эпики — `UMT-Epic-N.md`. Номер 1 каждого типа — канон, с которого копировать; живую задачу туда не писать. Дальше `N` — max существующего + 1.

Полная конвенция тикета — [[Tickets]]; она же описывает обязательный frontmatter, скелеты тела и правило `related:`. Проверка — `powershell -File Tools/ticket-format.ps1`.

Доска всех тикетов: [[Kanban]] — генерируется из frontmatter `status` через Dataview, править руками не нужно.

## Шаблон статьи

Любая статья `Architecture/`, `Recipes/` или `Process/` заканчивается секцией «Тикеты по системе» — она замыкает связь в обратную сторону: `related:` тикета ведёт в статью, а блок показывает эти тикеты в самой статье. Панель backlinks Obsidian этого не заменяет: главный потребитель связи — агент, который читает файл, а не UI.

Канонический вид блока — секция «Тикеты по системе» в конце любой статьи; `<Article>` в фильтре заменяется на имя файла статьи без расширения. Открытые и закрытые тикеты разведены двумя запросами: единая таблица на нагруженной системе становится нечитаемой, а смотрят в неё почти всегда ради открытых.

Готовый образец — конец этой же статьи, ниже.

## Obsidian Setup

Минимальный набор плагинов:

- Dataview — **обязателен**: индексы по `type`, `area`, `status`, `tags`, доска тикетов [[Kanban]] и блоки «Тикеты по системе» в статьях.
- Templater — шаблоны новых architecture/recipe/feature/adr статей.
- Linter — единый стиль markdown и frontmatter.
- Omnisearch — быстрый полнотекстовый поиск по vault.

Опционально:

- Tasks — если TODO по документации ведутся внутри Obsidian.
- QuickAdd — быстрое создание статей из шаблонов.
- Tag Wrangler — безопасное переименование тегов.
- Obsidian Git — если удобно коммитить документацию из Obsidian.

## Dataview Queries

Architecture:

```dataview
TABLE area, module, status, updated
FROM "Architecture"
SORT area ASC, module ASC
```

Recipes:

```dataview
TABLE area, module, status, updated
FROM "Recipes"
SORT module ASC
```

Process:

```dataview
TABLE area, module, status, updated
FROM "Process"
SORT module ASC
```

Статьи-заглушки:

```dataview
TABLE area, module, updated
FROM "Architecture" OR "Recipes" OR "Process"
WHERE status = "stub"
SORT module ASC
```

Tasks:

```dataview
TABLE status, kind, area, module, updated
FROM "Tasks"
SORT updated DESC
```

## Правила актуальности

Документация обновляется в том же изменении, что и код, если меняются:

- lifecycle, DI-регистрация или порядок инициализации;
- публичный контракт сервиса, view или data-класса;
- способ добавления новой фичи, окна, popup-а, save-data или провайдера;
- границы ответственности `Foundation` и `Features`;
- архитектурное решение, которое должен помнить будущий агент.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "00-Index") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "00-Index") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```