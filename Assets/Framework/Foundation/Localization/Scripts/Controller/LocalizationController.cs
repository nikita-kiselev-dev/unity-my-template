using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Scenes;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using VContainer;

namespace Framework.Foundation.Localization.Controller
{
    [AutoRegistration(Lifetime.Singleton)]
    [LifecycleOrder(SceneConstants.Scenes.Bootstrap, (int)BootstrapSceneInitOrder.LocalizationController)]
    [AutoLogger(nameof(LocalizationController))]
    public partial class LocalizationController : LifecycleEntity, ILocalizationController
    {
        // Инициализатор, а не null: VContainer перезапишет поле при инжекте, а без источников
        // (тесты, сборка без платформы) Init обязан не трогать LocalizationSettings вовсе.
        [Inject] private readonly IReadOnlyList<ILocaleSource> _localeSources = Array.Empty<ILocaleSource>();

        protected override async UniTask Load()
        {
            await LocalizationSettings.InitializationOperation.Task.AsUniTask();
        }

        protected override UniTask Init()
        {
            ApplyStartupLocale();
            SetEnabled(true);
            return UniTask.CompletedTask;
        }

        // Источники опрашиваются в Init, а не в Load: барьер фаз уже закрыл и инициализацию пакета,
        // и ожидание платформенного SDK, поэтому чтение языка синхронно и без гонки.
        private void ApplyStartupLocale()
        {
            foreach (var localeSource in _localeSources)
            {
                if (!localeSource.TryGetLocaleCode().TryGet(out var languageCode))
                {
                    continue;
                }

                // GetLocale сам делает и регистронезависимое сравнение, и фолбэк по цепочке
                // CultureInfo (ru-RU → ru), и отсев PseudoLocale — своего сопоставления не нужно.
                var locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
                if (locale == null)
                {
                    Logger.Log($"Locale '{languageCode}' is not available, keeping project default");
                    continue;
                }

                LocalizationSettings.SelectedLocale = locale;
                Logger.Log($"Startup locale set to '{locale.Identifier.Code}'");
                return;
            }
        }
    }
}
