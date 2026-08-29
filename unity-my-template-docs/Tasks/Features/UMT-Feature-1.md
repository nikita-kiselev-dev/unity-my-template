---
title: "Шаблон. Короткая формулировка работы"
type: task
kind: feature
status: Todo
area: Features
module: Clicker
related:
  - "[[Feature-Clicker]]"
created: 2026-08-29
updated: 2026-08-29
tags: [task, feature, features, clicker, template]
---

# Шаблон. Короткая формулировка работы

Скопируй файл в `UMT-Feature-N.md` (`N` = max существующего + 1). Этот файл не переводи
в `In Progress` и не правь под живую задачу. Если тикет — подтикет эпика, добавь во
frontmatter `epic: "[[UMT-Epic-N]]"`; если ждёт другой тикет — `blocked_by:`.

## Цель

Повторный клик в окне кликера не начисляет валюту, пока не истечёт кулдаун из конфига.

## Проблема

```
Сейчас: каждый тап по кнопке зовёт ClickerModel.Click и сразу пишет в IInventory.
        Серия тапов за кадр даёт пачку начислений, игрок фармит быстрее задуманного.
Хочу:   между двумя успешными Click проходит не меньше click_cooldown_seconds
        из ClickerConfig; UI показывает, что кнопка недоступна.
Почему: экономика кликера рассчитана на один клик в N секунд; без кулдауна
        баланс конфига бессмыслен.
```

## Скоуп

- [ ] Красный тест `ClickerModelTests.Click_DoesNotAdd_WhenCooldownActive` — падает на ассерте.
- [ ] `ClickerModel.Click`: ранний выход, если с прошлого успеха не прошло `click_cooldown_seconds` по `IClock.ServerUtcNow`.
- [ ] Поле `click_cooldown_seconds` в `ClickerConfig` + dummy-json.
- [ ] `ClickerViewModel`: `CanClick` / команда не исполняется на кулдауне; подписка `.AddTo(ref Subscriptions)`.
- [ ] Статья [[Feature-Clicker]]: инвариант кулдауна и как расширять.

Не входит: изменение формулы награды, сейв последнего клика между сессиями.

## Критерии Done

- [ ] `ClickerModelTests.Click_DoesNotAdd_WhenCooldownActive` зелёный; соседние тесты кликера не красные.
- [ ] `powershell -File Tools/fast-tests.ps1` зелёный.
- [ ] @user Компиляция и зелёный Test Runner
- [ ] @user В Core-сцене серия тапов начисляет валюту не чаще, чем раз в `click_cooldown_seconds`
