---
title: "Шаблон. Что сломано — одно предложение"
type: task
kind: bug
status: Todo
area: Foundation
module: Asset
related:
  - "[[Assets-Addressables]]"
created: 2026-08-29
updated: 2026-08-29
tags: [task, bug, foundation, asset, template]
---

# Шаблон. Что сломано — одно предложение

Скопируй файл в `UMT-Bug-N.md` (`N` = max существующего + 1). Этот файл не переводи
в `In Progress` и не правь под живую задачу.

## Симптом

Два одновременных `LoadAssetAsync` одного ключа: второй бросает
`InvalidOperationException: Already continuation registered`.
`InflightLoads.cs:12` — join-путь держит одну `Preserve()`-задачу на всех.

## Воспроизведение

Тест `InflightLoadsTests.Join_ResumesEveryWaiter_WhenLoadCompletes`: два `Join`
на один ключ до `Complete`. Падает на втором `OnCompleted`. Без теста — два
параллельных `IconProvider` запроса одного атласа при открытии окна.

## Причина

`UniTask.Preserve()` запоминает результат, но до завершения допускает ровно один
continuation. Join-путь нужен для нескольких ожидающих **одновременно**. Корневая
причина — выбор примитива, а не гонка вызывающих.

## Решение

`InflightLoads<T>` на `UniTaskCompletionSource<T>` (`secondaryContinuationList`).
`Begin` / `Join` / `Complete` / `Fail`. Провал не мемоизируется. Тест покрывает
несколько ждущих, отмену одного и несовпадение типа до ожидания.

## Критерии Done

- [ ] `InflightLoadsTests` зелёный на двух и более одновременных `Join`.
- [ ] `powershell -File Tools/fast-tests.ps1` зелёный.
- [ ] @user Компиляция и зелёный Test Runner
