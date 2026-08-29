---
title: Audio
type: architecture
area: Foundation
module: Audio
status: actual
source_paths:
  - Assets/Framework/Foundation/Audio/
  - Assets/Framework/Features/Audio/
  - Assets/Framework/Foundation/Initialization/Scripts/Scopes/RootScope.cs
related:
  - "[[Foundation-vs-Features]]"
  - "[[Assets-Addressables]]"
  - "[[Ads]]"
  - "[[UI-MVVM]]"
tags:
  - architecture
  - audio
  - foundation
updated: 2026-08-08
---

# Audio

## Для агента

Открывай эту статью, если нужно проиграть звук или музыку, добавить новый клип, разобраться, почему
звук не слышно, или понять, кто и когда глушит игру.

Короткий ответ на «как проиграть»:

```csharp
[Inject] private readonly IAudioController _audioController;

_audioController.PlaySound(SoundKeys.ClickSound0);
_audioController.PlayMusic(MusicKeys.CoreSceneMusic);
```

Строка — это **ключ Addressables**, он же имя файла клипа. Метод не async и ничего не возвращает:
загрузка и ожидание готовности спрятаны внутрь.

Главная неочевидность, из-за которой «звук не играет»: `PlaySound`/`PlayMusic` **ждут, пока кто-то
выставит громкость**. Пока `SetSoundsVolume`/`SetMusicVolume` не вызван ни разу, вызов висит в
`UniTask.WaitUntil` (`AudioController.cs:57-67`). В шаблоне громкость применяет `SettingsViewModel`
на старте — из сохранённых настроек.

## Назначение

Подсистема даёт фиче один синхронный фасад вместо связки «загрузи клип → дождись → найди
`AudioSource` → проиграй». Всё, что фича знает о звуке, — это `IAudioController` и строковый ключ.

## Ключевые типы

| Тип | Роль |
| --- | --- |
| `IAudioController` / `AudioController` | фасад подсистемы; `MonoBehaviour` на префабе `RootScope` |
| `IAudioClipLoader` / `AudioClipLoader` | загрузка клипа поверх `IAssetProvider`, Singleton |
| `AudioSourcePlayer` | база проигрывателя: один `AudioSource`, кэш клипов, громкость |
| `SoundPlayer` | `PlayOneShot`, короткие звуки |
| `MusicPlayer` | зацикленная музыка, кроссфейд при смене трека |
| `ButtonSoundBinder` | `LifecycleEntity` + `IViewSetupStep`: прогрев клик-звука и его навеска на кнопки созданного view |
| `SoundKeys` / `MusicKeys` | ключи клипов |
| `AudioConstants` | длительности фейдов музыки |

Микшера (`AudioMixer`, группы, снапшоты) в шаблоне **нет**: громкость выставляется прямо на двух
`AudioSource` (`AudioSourcePlayer.cs:35-38`). Это осознанный минимум — две шкалы, звук и музыка,
ровно столько, сколько показывает окно настроек. Когда понадобятся эффекты, дакинг или третья
шкала, микшер добавляется под тем же фасадом (см. «Как расширять»).

## Жизненный цикл и владение

`AudioController` — компонент на префабе `RootScope`, а не `LifecycleEntity`. Отсюда три следствия:

- Он **не проходит фазы** `Load`/`Init`/`PostInit` и выставляет статусы сам: `SetEnabled(true)`
  и `SetInited(true)` прямо в `Awake` (`AudioController.cs:69-84`). Собственный `EntityStatus` он
  держит потому, что реализует `IEntityStatus` наравне с остальными системными компонентами, — см.
  [[Initialization-LifecycleEntity]] и [[Utilities]].
- Он живёт через сцены: `DontDestroyOnLoad(this)` в `Awake`, а сам `Awake` идемпотентен (ранний
  выход по `Status.IsInited`).
- `RootScope` находит его через `FindAnyObjectByType` и падает с `InvalidOperationException`, если
  на префабе его нет (`RootScope.cs:37-43`). Молчаливый скип отложил бы падение до резолва первого
  потребителя.

Ссылки на `SoundPlayer` и `MusicPlayer` — сериализованные поля префаба (`m_SoundSource`,
`m_MusicSource`); в `Awake` каждому отдаётся загрузчик через `Init(_audioLoader)`.

## Загрузка клипов и владение ассетом

`AudioClipLoader` — тонкая обёртка над `IAssetProvider.LoadAssetAsync<AudioClip>`; отсутствие клипа
превращается в исключение с именем ключа (`AudioClipLoader.cs:15-19`). Это один из немногих классов
`Foundation`, которым нужен полный `IAssetProvider`, а не `IAssetScope`, — именно из-за `persistent`,
см. [[Assets-Addressables]].

Флаг `persistent` разводит музыку и звуки:

| | `persistent` | Кэш в `AudioSourcePlayer` | Что происходит под шторкой |
| --- | --- | --- | --- |
| Музыка (`MusicPlayer.Play`) | `true` по умолчанию | да | клип переживает смену сцены |
| Звук (`SoundPlayer.Play`) | `false` | **нет** | клип освобождается и грузится заново |

Не-persistent клип сознательно **не кладётся в кэш** (`AudioSourcePlayer.cs:50-55`): провайдер
освободит его по `LoadingCurtainShownSignal`, а запись в словаре превратилась бы в fake-null —
Unity-объект уничтожен, ссылка не `null`. Проверка `audioClip &&` в `GetAudio` страхует тот же
случай для persistent-записей.

`ButtonSoundBinder` грузит клик-звук в фазе `Load` **без** `persistent` (`ButtonSoundBinder.cs:28`),
и делает это на каждой сцене заново — он объявлен на `Start`, `Core` и `Meta` с порядком `Last`.
Именно поэтому он остаётся `LifecycleEntity`, а не обычным Singleton-сервисом: не-persistent клип
освобождается по шторке, и прогрев нужен на каждой сцене.

Навеска звука идёт через `IViewSetupStep` (см. [[UI-Views]]): тот же класс реализует `Setup(MonoView)`
и вешает обработчик на все `Button` внутри созданного view, включая неактивные. Обработчик ставится
через `AddListenerClean` — сначала `RemoveListener`, потом `AddListener`, поэтому повторный проход
не задваивает звук.

Два следствия смены механизма (было — `FindObjectsByType<Button>` по всей сцене в `PostInit`):

- View, созданные **после** `PostInit` (попап-заглушка рекламы, префабы дней `DailyBonus`), теперь
  звук получают — раньше сканирование их не заставало.
- Кнопки, которые не приезжают через `ViewFactory`, звука не получают. В шаблоне такая одна —
  фон-затемнение на `PopupCanvas` (его инстанцирует `CanvasProvider` напрямую через `IAssetProvider`).
  Статичных кнопок в сценах шаблона нет.

## Громкость, mute и настройки

`SetSoundsVolume` / `SetMusicVolume` пишут прямо в `AudioSource.volume` и поднимают внутренний флаг
«громкость выставлена». Геттеров громкости в контракте **нет** — состояние живёт в `SettingsModel`,
а не в аудио-подсистеме. Связка односторонняя и описана в [[UI-MVVM]]:

```csharp
_model.SoundsVolume.Subscribe(audioController.SetSoundsVolume).AddTo(ref Subscriptions);
```

`ReactiveProperty` реплеит текущее значение при подписке — та же строка применяет сохранённую
громкость на старте, отдельного «применить настройки» не нужно (`SettingsViewModel.cs:19-24`).

`SetMuted(bool)` глушит **всю** игру целиком через `AudioListener.pause`, а не через выставление
громкостей: восстанавливать нечего, геттеров нет. Единственный вызывающий — `AdsController` на
границах ad-сессии (см. [[Ads]]); `Time.timeScale` при этом не трогается.

## Кто что играет

| Событие | Кто вызывает |
| --- | --- |
| Музыка сцены `Start` | `StartSceneState.OnLoaded` |
| Музыка сцены `Core` | `CoreSceneState.OnLoaded` |
| Клик по кнопке внутри любого view | `ButtonSoundBinder` (как `IViewSetupStep`) |
| Громкость из настроек | `SettingsViewModel` |
| Mute на время рекламы | `AdsController` |

Смена трека, когда музыка уже играет, идёт через `PrimeTween`-последовательность: fade-out до нуля,
подмена клипа, fade-in (`MusicPlayer.cs:42-50`), длительности — в `AudioConstants.Parameters`.
Повторный запрос того же трека игнорируется по имени клипа (`MusicPlayer.cs:16-21`).

## Граница Foundation / Features

Вся логика — в `Foundation/Audio/`. В `Features/Audio/` лежат **только ассеты**: `Music/` и `Sounds/`,
скриптов там нет. Ключи при этом объявлены в `Foundation` (`SoundKeys`, `MusicKeys`) — потому что
`ButtonSoundBinder` и `*SceneState` живут в `Foundation` и обязаны компилироваться без `Features`.

Ключи конкретной игровой механики так объявлять не нужно: они идут в `<Feature>Constants` рядом с
фичей, как и остальные ключи Addressables (см. [[Naming]]).

## Инварианты

- Фича не создаёт `AudioSource` и не обращается к `AudioListener` — только `IAudioController`.
  Проверка: `grep -rn "AudioSource\|AudioListener" Assets/Framework --include=*.cs` вне `Tests/` даёт
  попадания только в `Foundation/Audio/`.
- Клип грузится только через `IAudioClipLoader` — прямого `Addressables.*` и `Resources.Load` нет.
- Значение `SoundKeys`/`MusicKeys` совпадает с именем файла клипа в `Features/Audio/`, оно же адрес
  Addressables (правило `addressable-address-mismatch` в `Tools/naming-check.ps1`).
- Не-persistent клип не попадает в кэш `AudioSourcePlayer`.
- На префабе `RootScope` есть `AudioController` — иначе `RootScope.RegisterSceneComponents` бросает
  на старте.
- `AudioController.Awake` идемпотентен: повторный вызов после `DontDestroyOnLoad` не переинициализирует
  проигрыватели.
- Заглушить игру можно только через `SetMuted`; `AudioListener.pause` вне `AudioController` не
  выставляется.

## Как расширять

**Новый звук или трек.** Файл в `Features/Audio/Sounds/` или `Music/`, имя = ключ (PascalCase),
запись в Addressables, константа в `SoundKeys`/`MusicKeys` или в `<Feature>Constants` для фичевого
звука. Кода менять не нужно.

**Третья шкала громкости** (например голос): ещё один `AudioSourcePlayer` на префабе, поле в
`AudioController`, пара `Play*`/`SetVolume` в `IAudioController`, свойство в `SettingsModel` и
биндинг во `SettingsViewModel`. Это пять точек — признак того, что при четвёртой шкале пора
переходить на микшер.

**`AudioMixer`.** Заводится под фасадом: `AudioSource` подключаются к группам, `SetSoundsVolume`
начинает писать в exposed-параметр вместо `AudioSource.volume`. Контракт `IAudioController` при этом
не меняется, потребители не трогаются. Тогда же уместно перевести `SetMuted` со `AudioListener.pause`
на снапшот — это единственный способ заглушить не всё сразу.

**Пул источников** для одновременных звуков: сейчас `SoundPlayer` — один `AudioSource` с
`PlayOneShot`, наложение работает, но громкостью отдельных звуков управлять нельзя. Пул добавляется
внутрь `SoundPlayer`, контракт не меняется.

**Пауза звука на паузе приложения** сейчас не реализована: `ApplicationPauseChangedSignal` слушают
`ProgressSaver` и `Clock`, но не аудио (см. [[Signals]]). Мобильная платформа глушит звук сама;
если понадобится явное поведение — подписка добавляется в `AudioController`, не в фичи.

## Тесты

Собственных тестов у подсистемы нет: `AudioController` — `MonoBehaviour` с зависимостью от
`AudioSource`, а PlayMode-тесты в шаблоне не пишутся (см. [[Testing-TDD]]). Потребители тестируются
с фейком `FakeAudioController` (`Foundation/Tests/Fakes/`) — так проверяются `SettingsViewModel`,
`SceneStateMachine` и `AdsController`.

Отсюда практическое правило: логика, которую хочется протестировать, не должна оказываться внутри
`AudioController`. Он остаётся фасадом без решений.

## Когда обновлять

- Изменился контракт `IAudioController` (новый метод, новая шкала громкости).
- Появился `AudioMixer` или пул источников — раздел «Ключевые типы» и «Как расширять» устареют сразу.
- Изменилось правило `persistent` для клипов или кэширование в `AudioSourcePlayer`.
- `AudioController` перестал быть компонентом `RootScope` или стал `LifecycleEntity`.
- Появился новый вызывающий `SetMuted` кроме `AdsController`.
- Добавлены ключи в `SoundKeys` / `MusicKeys`.

## Last Verified

2026-08-08, against current project state.

## Тикеты по системе

Тикеты, у которых в `related:` стоит ссылка на эту статью. Пустая таблица — сигнал: либо
система мёртвая, либо у её тикетов не проставлен `related:`.

Открытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Audio") AND (status = "Todo" OR status = "In Progress")
SORT updated DESC
```

Закрытые:

```dataview
TABLE WITHOUT ID file.link AS "Тикет", title, kind, status, updated
FROM "Tasks"
WHERE type = "task" AND contains(string(related), "Audio") AND (status = "Done" OR status = "Cancelled")
SORT updated DESC
```
