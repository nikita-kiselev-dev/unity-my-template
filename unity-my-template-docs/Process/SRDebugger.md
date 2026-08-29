---
title: SRDebugger
type: process
area: Cross-cutting
module: SRDebugger
status: actual
source_paths: []
related:
  - "[[Foundation-vs-Features]]"
  - "[[Initialization-LifecycleEntity]]"
  - "[[Assets-Addressables]]"
  - "[[Ads]]"
tags:
  - process
  - srdebugger
  - tooling
  - integrations
updated: 2026-08-29
---

## Для агента

**Дев-оверлея SRDebugger в шаблоне больше нет.** Плагин платный (Asset Store, StompyRobot), и
вместе с ним выпилены `Assets/StompyRobot/`, `Framework.Integrations.SRDebugger` и панели
`SROptions.*`.

Статья оставлена надгробием: `module: SRDebugger` из её frontmatter живёт только здесь —
удаление файла выкинет модуль из словаря `ticket-format.ps1 -ListModules`, а платный
плагин нельзя возвращать в шаблон молча.

Чем смотреть рантайм-состояние теперь:

| Что нужно | Куда идти |
| --- | --- |
| Логи подсистемы, консоль редактора | [[Logger]], `unity cmd get_console_logs` ([[Unity-CLI]]) |
| Состояние ассет-провайдера | `IAssetProviderDiagnostics.GetSnapshot()` ([[Assets-Addressables]]) |
| Таблица статусов entity сцены | `LifecycleSceneSelector.SelectForScene` ([[Initialization-LifecycleEntity]]) |
| Ручной показ рекламы | `EditorAdsProvider` — попап-заглушка Success/Fail ([[Ads]]) |

## Инварианты

- Платные плагины в репозиторий не возвращаются: шаблон едет в другие проекты через subtree.
- Точки съёма диагностики (`IAssetProviderDiagnostics`, `LifecycleSceneSelector`) оставлены
  специально — свой оверлей пишется поверх них отдельным asmdef в `Integrations/`.

## Когда обновлять

Если в проект придёт свой дев-оверлей — переписать статью под него и вернуть `source_paths`.
