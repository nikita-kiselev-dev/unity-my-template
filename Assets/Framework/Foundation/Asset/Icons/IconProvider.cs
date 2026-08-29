using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.Utilities.Extensions;
using R3;
using UnityEngine;
using UnityEngine.U2D;
using VContainer;

namespace Framework.Foundation.Asset.Icons
{
    // Singleton, потому что провайдер держат Singleton-потребители фич. Ассеты между
    // сценами при этом не переезжают: иконки грузятся не-persistent и кэш чистится по шторке.
    [AutoRegistration(Lifetime.Singleton)]
    [AutoLogger(nameof(IIconProvider))]
    public partial class IconProvider : IIconProvider, IDisposable
    {
        private readonly IAssetProvider _assetProvider;

        private readonly Dictionary<string, Sprite> _icons = new();
        private readonly Dictionary<string, SpriteAtlas> _iconAtlases = new();

        private DisposableBag _subscriptions;

        // Зависимости через ctor, а не [Inject]-поля: подписке на шторку нужна точка входа, а
        // [Inject]-метод у типа уже занят генератором [AutoLogger] — двух таких методов быть не должно.
        public IconProvider(ISignalBus signalBus, IAssetProvider assetProvider)
        {
            _assetProvider = assetProvider;
            SpriteAtlasManager.atlasRequested += OnAtlasRequested;
            signalBus.Subscribe<LoadingCurtainShownSignal>(ClearCache).AddTo(ref _subscriptions);
        }

        public async UniTask<Sprite> GetIcon(string iconName, CancellationToken cancellationToken = default)
        {
            if (_icons.TryGetValue(iconName, out var icon) && icon)
            {
                return icon;
            }

            _icons.Remove(iconName);
            icon = await _assetProvider.LoadAssetAsync<Sprite>(iconName, cancellationToken: cancellationToken);
            _icons[iconName] = icon;
            return icon;
        }

        public async UniTask<Sprite> GetIconFromAtlas(string iconName, string iconTypeName = null, CancellationToken cancellationToken = default)
        {
            if (iconTypeName.IsNullOrEmpty())
            {
                iconTypeName = IconConstants.Types.DefaultIcons;
            }

            var iconAtlas = await GetAtlas(iconTypeName, cancellationToken);
            var icon = iconAtlas.GetSprite(iconName);

            if (icon != null)
            {
                return icon;
            }

            Logger.LogError($"Can't find {iconName} in {iconTypeName} atlas, so trying to use {nameof(GetIcon)} method instead.");
            icon = await GetIcon(iconName, cancellationToken);
            return icon;
        }

        public async UniTask<SpriteAtlas> GetAtlas(string iconTypeName, CancellationToken cancellationToken = default)
        {
            var atlasName = IconConstants.Formats.AtlasName.UseAsFormat(iconTypeName);

            if (_iconAtlases.TryGetValue(atlasName, out var atlas) && atlas)
            {
                return atlas;
            }

            _iconAtlases.Remove(atlasName);
            var iconAtlas = await _assetProvider.LoadAssetAsync<SpriteAtlas>(atlasName, cancellationToken: cancellationToken);
            _iconAtlases[atlasName] = iconAtlas;
            return iconAtlas;
        }

        private void OnAtlasRequested(string atlasName, Action<SpriteAtlas> callback)
        {
            if (_iconAtlases.TryGetValue(atlasName, out var cachedAtlas))
            {
                callback?.Invoke(cachedAtlas);
                return;
            }
            
            _assetProvider.LoadAssetAsync<SpriteAtlas>(atlasName).ContinueWith(loadedAtlas =>
            {
                if (loadedAtlas != null)
                {
                    _iconAtlases[atlasName] = loadedAtlas;
                    callback?.Invoke(loadedAtlas);
                }
                else
                {
                    Logger.LogError($"Failed to load sprite atlas: {atlasName}.");
                }
            }).Forget();
        }

        // Шторка уже освободила все не-persistent ассеты: в словарях остались бы записи с
        // fake-null, которые молча перезагружались бы поштучно.
        private void ClearCache()
        {
            _icons.Clear();
            _iconAtlases.Clear();
        }

        void IDisposable.Dispose()
        {
            SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
            _subscriptions.Dispose();

            foreach (var icon in _icons)
            {
                _assetProvider.ReleaseAsset(icon.Key);
            }

            foreach (var iconAtlas in _iconAtlases)
            {
                _assetProvider.ReleaseAsset(iconAtlas.Key);
            }

            ClearCache();
        }
    }
}
