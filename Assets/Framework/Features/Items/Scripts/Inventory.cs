using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset.Icons;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Features.Items.Data;
using Framework.Features.Items.Configs;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using VContainer;

namespace Framework.Features.Items
{
    [AutoRegistration(Lifetime.Singleton)]
    [LifecycleOrder(SceneConstants.Scenes.Bootstrap, (int)BootstrapSceneInitOrder.Inventory)]
    [AutoLogger(ItemsConstants.LogName)]
    public partial class Inventory : LifecycleEntity, IInventory
    {
        [Inject] private readonly IIconProvider _iconProvider;
        [Inject] private readonly ItemsData _itemsData;
        [Inject] private readonly CurrenciesConfig _config;

        private Dictionary<string, IItemCounter> _counters;

        public bool TryGetCounter(string key, out IItemCounter counter)
        {
            return _counters.TryGetValue(key, out counter);
        }

        // Валютные операции — хот-пасс: раскраска ключа и значений раньше считалась даже
        // на успешном пути, поэтому форматирование живёт только внутри LogOperation.
        public bool Add(ItemOperation operation)
        {
            if (!_counters.TryGetValue(operation.Key, out var counter))
            {
                Logger.LogError($"Can't add item. Item {operation.Key.SetFeatureColor()} does not exist.");
                return false;
            }

            var isSuccess = counter.Add(operation.Value);

            if (!isSuccess)
            {
                return false;
            }

            LogOperation("added", operation, counter);

            return true;
        }

        public bool Remove(ItemOperation operation)
        {
            if (!_counters.TryGetValue(operation.Key, out var counter))
            {
                Logger.LogError($"Can't remove item. Item {operation.Key.SetFeatureColor()} does not exist.");
                return false;
            }

            var isSuccess = counter.Remove(operation.Value);

            if (!isSuccess)
            {
                return false;
            }

            LogOperation("removed", operation, counter);

            return true;
        }

        public bool IsEnough(ItemOperation operation)
        {
            if (_counters.TryGetValue(operation.Key, out var counter))
            {
                return counter.Info.Value.CurrentValue >= operation.Value;
            }

            Logger.LogError($"Can't compare values. Item {operation.Key.SetFeatureColor()} does not exist.");
            return false;
        }

        protected override async UniTask Init()
        {
            var factory = new ItemCounterFactory(_config, _iconProvider, _itemsData);
            _counters = await factory.CreateAll(CancellationToken);
        }

        private void LogOperation(
            string operationName,
            ItemOperation operation,
            IItemCounter counter)
        {
            if (!Logger.AreLogsEnabled)
            {
                return;
            }

            var keyString = operation.Key.SetFeatureColor();
            var operationString = operationName.SetFeatureColor();
            var valueString = operation.Value.ToString().SetFeatureColor();
            var currentValueString = counter.Info.Value.CurrentValue.ToString().SetFeatureColor();
            Logger.Log($"\nOperation: {operationString} \nKey: {keyString} \nValue: {valueString} \nNew Current Value: {currentValueString}");
        }
    }
}
