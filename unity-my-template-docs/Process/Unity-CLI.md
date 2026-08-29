---
title: Unity CLI
type: process
area: Project
module: Unity CLI
status: actual
source_paths:
  - Packages/manifest.json
related:
  - "[[Testing-TDD]]"
  - "[[Hooks]]"
  - "[[Tickets]]"
tags:
  - process
  - tooling
  - unity-cli
updated: 2026-08-15
---

# Unity CLI

## Для агента

Открывай статью, когда задача упирается в **открытый Unity Editor**: посмотреть консоль после
правки, дождаться перекомпиляции, прогнать настоящий Test Runner, снять состояние сцены или
проверить, применился ли ассет.

Канал не заменяет `Tools/fast-tests.ps1`: тот работает при закрытом редакторе и отвечает за
секунды ([[Testing-TDD]]). Unity CLI отвечает на другой вопрос — «что сейчас думает живой
редактор», и требует, чтобы редактор был запущен.

Первое действие всегда одно: `unity status`. Всё остальное имеет смысл только при `state: ready`.

## Как устроено

Две независимые части:

- **`unity` CLI** — отдельная программа Unity (`unity --version` → `1.0.0-beta.3`), лежит вне
  проекта: `%LOCALAPPDATA%\Unity\bin\unity.exe`, доступна в `PATH` как `unity`. Ставится и
  обновляется пользователем (`unity upgrade`), в репозиторий не входит.
- **`com.unity.pipeline`** — UPM-пакет в `Packages/manifest.json` (сейчас `0.4.0-exp.1`). Это он
  поднимает внутри открытого редактора локальный HTTP-API и регистрирует набор команд. Без пакета
  CLI видит процесс Unity, но подключиться не может.

Транспорт — HTTP на `127.0.0.1`, порт по умолчанию `7800`. Порт локальный: снаружи машины канала
нет. Проект определяется автоматически по текущей директории; при нескольких открытых редакторах
цель задаётся явно через `--project-path`.

Пакет уже стоит в манифесте — `unity pipeline install` для этого проекта повторять не нужно.

## Проверка связи

```bash
unity status
```

```
Port	State	Project	Version	PID
7800	ready	D:\Development\Unity\unity-my-template	6000.3.14f1	35300
```

Редактор закрыт — таблица пуста. Редактор открыт, но строки нет — пакет не поднялся; это ручной
шаг пользователя, а не то, что агент чинит правкой манифеста.

## Каталог инструментов

Список команд редактор отдаёт сам, вместе со схемой параметров:

```bash
unity list --json
```

`.data.tools` — массив из ~140 записей вида `{ name, description, group, parameters[] }`, у каждого
параметра `name`, `type`, `required`, `default`. **Схема — единственный источник истины про имена
параметров**: онлайн-справочник Unity отстаёт от беты, а локальный вывод соответствует тому, что
установлено.

Крупные группы: сцены и GameObject-ы, префабы, материалы и шейдеры, ассеты и импорт, пакеты,
Animator и AnimationClip, запекание света / NavMesh / occlusion, настройки проекта, билд,
консоль, компиляция, тесты, play mode, скриншот Scene View.

## Вызов инструмента

```bash
unity cmd recompile_status --json
```

```bash
unity cmd get_console_logs --severity=error --limit=20 --json
```

Параметры инструмента передаются как `--<имя>=<значение>` вперемешку с флагами самого CLI.
Полезное лежит в `.data.result`; `.data.target` подтверждает, в какой редактор ушёл вызов.
Код возврата `0` означает, что вызов доставлен, — про успех операции судят по `success` и по
содержимому `result`.

Форма `result` **не единообразна**: у одних инструментов это объект (`get_console_logs`,
`run_tests`), у других — строка с JSON внутри (`recompile_status`, `test_status`), которую нужно
распарсить вторым проходом. Проверять, а не предполагать.

## Что это даёт агенту

| Задача | Команда |
| --- | --- |
| Компилируется ли проект прямо сейчас | `unity cmd recompile_status` → `status`, `failed`, `errors[]` |
| Заставить редактор пересобрать скрипты | `unity cmd recompile`, затем опрос `recompile_status` |
| Ошибки и предупреждения консоли | `unity cmd get_console_logs --severity=error --limit=50` |
| Настоящий Test Runner | `unity cmd run_tests --mode=editor --async_tests=true`, затем `test_status` |
| Какие тесты вообще есть | `unity cmd list_tests --mode=editor` |
| Точечный прогон | `run_tests --filter=<имя> --filter_type=testName\|assembly\|category` |

Замер на этом проекте: `run_tests --mode=editor` → 374 теста, 374 passed, 4.11 с. Это полноценный
Test Runner в живом редакторе, а не эмуляция.

Асинхронный режим предпочтителен: `async_tests=true` возвращает управление сразу, результат
забирается опросом `test_status`, и долгий прогон не упирается в `--timeout` вызова (по умолчанию
30 с у `unity cmd`).

## Инварианты

- Редактор должен быть открыт и `ready`. Ничего из перечисленного не работает «в фоне».
- **Неизвестный параметр не является ошибкой.** CLI передаёт его дальше, инструмент молча
  игнорирует, вызов возвращает `success: true` с результатом по умолчанию. Проверено: `--count=2
  --type=Error` у `get_console_logs` вернули все записи всех типов, потому что параметры называются
  `limit` и `severity`. Отсюда правило: имена берутся из `unity list --json`, а результат
  сверяется с ожиданием, а не принимается по коду возврата.
- Разрушающие инструменты требуют `confirm=true` (`delete_asset`, `clear_baked_lighting`,
  `switch_build_target`, перезапись в `write_text_file`, `package_add` / `package_remove`), у части
  есть `dry_run`. Это защита пакета, а не повод её обходить: правки в `Assets/` агент делает
  файловыми инструментами и тикетом, а не удалённым вызовом.
- Канал не отменяет `@user`-приёмку из [[Tickets]]. Компиляцию и тесты через него проверить можно;
  «сцена запускается и выглядит правильно» — нельзя.
- Канал не входит в Stop-цепочку [[Hooks]]: редактор бывает закрыт, а прогон идёт минуты. Гейт,
  который зависит от внешнего процесса, ломается тише, чем защищает.

## Границы

Через CLI **не** делается то, что уже покрыто проектной оснасткой:

- быстрая проверка компиляции и тестов — `Tools/fast-tests.ps1` (секунды, редактор не нужен);
- правка исходников и ассетов — обычные файловые инструменты плюс заявленный скоуп;
- проверки имён, документации, тикетов — соответствующие `Tools/*.ps1`.

Плеерный билд (`unity cmd build`, `unity build`) и запуск отдельного редактора в batch-режиме
(`unity test <project>`) в этом проекте не проверялись: при открытом редакторе проект залочен, и
batch-прогон конфликтует с ним. Нужен билд — это ручной шаг пользователя.

## Когда обновлять

- Сменилась версия `com.unity.pipeline` в `Packages/manifest.json` или состав команд в
  `unity list`.
- Обновился `unity` CLI и изменился синтаксис `cmd` / `status` / `list` либо форма JSON-ответа.
- Появился хук или скрипт в `Tools/`, использующий канал.
- Изменилось правило `@user`-приёмки в [[Tickets]] в части «компиляция и Test Runner».

## Last Verified

2026-08-15, against current project state: `unity 1.0.0-beta.3`, `com.unity.pipeline 0.4.0-exp.1`,
редактор `6000.3.14f1` на порту 7800.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Unity-CLI") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Unity-CLI") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
