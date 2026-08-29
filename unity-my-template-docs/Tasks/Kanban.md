---
title: Доска тикетов
type: board
area: Project
module: Documentation
status: actual
updated: 2026-08-08
tags:
  - kanban
  - tasks
  - dataview
---

# Доска тикетов

Доска полностью выводится из frontmatter тикетов в `Tasks/`. Единственный источник
истины — поле `status` каждого тикета (`Todo` / `In Progress` / `Done` / `Cancelled`). Чтобы тикет
появился в колонке или переехал между ними, меняй только `status` в самом тикете —
этот файл править не нужно.

Эпики вынесены в отдельную секцию сверху и из четырёх обычных колонок исключены: эпик живёт
месяцами и объединяет чужую работу, поэтому в потоке «сделать за сессию» он только шумит.
Формат тикета — [[Tickets]].

Требуется включённый плагин **Dataview**: без него блоки ниже отрендерятся как код.

## Эпики

```dataview
TABLE WITHOUT ID file.link AS "Эпик", title, status, module, updated
FROM "Tasks"
WHERE type = "task" AND kind = "epic"
SORT status ASC, file.name ASC
```

Прогресс по эпику:

```dataview
TABLE WITHOUT ID epic AS "Эпик", length(rows) AS "Всего",
  length(filter(rows, (r) => r.status = "Done" OR r.status = "Cancelled")) AS "Закрыто"
FROM "Tasks"
WHERE type = "task" AND epic
GROUP BY epic
SORT epic ASC
```

Список подтикетов конкретного эпика — dataview-блок «Подтикеты» в самом эпике.

## Todo

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, area, module, updated
FROM "Tasks"
WHERE type = "task" AND kind != "epic" AND status = "Todo"
SORT updated DESC
```

## In Progress

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, area, module, updated
FROM "Tasks"
WHERE type = "task" AND kind != "epic" AND status = "In Progress"
SORT updated DESC
```

## Done

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, area, module, updated
FROM "Tasks"
WHERE type = "task" AND kind != "epic" AND status = "Done"
SORT updated DESC
```

## Cancelled

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, area, module, updated
FROM "Tasks"
WHERE type = "task" AND kind != "epic" AND status = "Cancelled"
SORT updated DESC
```
