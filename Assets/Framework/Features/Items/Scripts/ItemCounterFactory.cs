using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset.Icons;
using Framework.Features.Items.Data;
using Framework.Features.Items.Configs;
using Framework.Foundation.Localization.Extensions;
using Framework.Foundation.Utilities.Extensions;
using UnityEngine;

namespace Framework.Features.Items
{
    public class ItemCounterFactory : IItemCounterFactory
    {
        private readonly CurrenciesConfig _config;
        private readonly IIconProvider _iconProvider;
        private readonly ItemsData _itemsData;

        public ItemCounterFactory(
            CurrenciesConfig config,
            IIconProvider iconProvider,
            ItemsData itemsData)
        {
            _config = config;
            _iconProvider = iconProvider;
            _itemsData = itemsData;
        }

        public async UniTask<Dictionary<string, IItemCounter>> CreateAll(CancellationToken cancellationToken = default)
        {
            var currencies = _config.Currencies;
            var loads = new UniTask<Presentation>[currencies.Count];

            for (var i = 0; i < currencies.Count; i++)
            {
                loads[i] = Load(currencies[i], cancellationToken);
            }

            // Порядок записи в ItemsData остаётся порядком конфига: параллельна только загрузка,
            // сами счётчики собираются последовательно из готовых результатов.
            var presentations = await UniTask.WhenAll(loads);
            var counters = new Dictionary<string, IItemCounter>(currencies.Count);

            for (var i = 0; i < currencies.Count; i++)
            {
                var currency = currencies[i];
                var presentation = presentations[i];

                _itemsData.AddNewItem(currency);
                counters.Add(currency, new ItemCounter(
                    _itemsData, currency, presentation.Name, presentation.Description, presentation.Icon));
            }

            return counters;
        }

        private async UniTask<Presentation> Load(string currency, CancellationToken cancellationToken)
        {
            var nameKey = ItemsConstants.Formats.Name.UseAsFormat(currency);
            var descriptionKey = ItemsConstants.Formats.Description.UseAsFormat(currency);
            var iconKey = IconConstants.Formats.IconName.UseAsFormat(currency);

            var (name, description, icon) = await UniTask.WhenAll(
                nameKey.Localize(ItemsConstants.Localization.Currencies).AttachExternalCancellation(cancellationToken),
                descriptionKey.Localize(ItemsConstants.Localization.Currencies).AttachExternalCancellation(cancellationToken),
                _iconProvider.GetIconFromAtlas(iconKey, currency, cancellationToken));

            return new Presentation(name, description, icon);
        }

        private readonly struct Presentation
        {
            public Presentation(string name, string description, Sprite icon)
            {
                Name = name;
                Description = description;
                Icon = icon;
            }

            public string Name { get; }
            public string Description { get; }
            public Sprite Icon { get; }
        }
    }
}
