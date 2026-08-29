using System;
using Framework.Features.Clicker.Data;
using Framework.Features.Clicker.Configs;
using Framework.Features.Items;
using Framework.Foundation.Logger;
using R3;

namespace Framework.Features.Clicker.Model
{
    public class ClickerModel : IDisposable
    {
        private readonly ClickerData _data;
        private readonly IInventory _inventory;
        private readonly ILogChannel _logger;

        private readonly int _maxLevel;
        private readonly ReactiveProperty<int> _level;
        private readonly ReadOnlyReactiveProperty<ClickerLevelConfig> _currentLevelConfig;
        private readonly ReadOnlyReactiveProperty<bool> _canUpgrade;

        public ReadOnlyReactiveProperty<int> Level => _level;
        public ReadOnlyReactiveProperty<ClickerLevelConfig> CurrentLevelConfig => _currentLevelConfig;
        public ReadOnlyReactiveProperty<bool> CanUpgrade => _canUpgrade;

        public ClickerModel(IClickerConfig config, ClickerData data, IInventory inventory, ILogChannel logger)
        {
            _data = data;
            _inventory = inventory;
            _logger = logger;
            _maxLevel = config.Levels.Count - 1;

            _level = new ReactiveProperty<int>(ClampLevel(data.Level));
            _currentLevelConfig = _level.Select(level => config.Levels[level]).ToReadOnlyReactiveProperty();
            _canUpgrade = _level.Select(level => config.Levels.Count > level + 1).ToReadOnlyReactiveProperty();
        }

        // Сейв и конфиг расходятся, когда уровни урезали после прогресса игрока или сейв битый:
        // читать конфиг по такому индексу — падение фичи на старте.
        private int ClampLevel(int level)
        {
            if (level >= 0 && level <= _maxLevel)
            {
                return level;
            }

            var clamped = Math.Clamp(level, 0, _maxLevel);
            _logger.LogError($"Clicker level {level} is out of config range, clamped to {clamped}.");
            return clamped;
        }

        public void Click()
        {
            _data.OnClick();
            _inventory.Add(new ItemOperation(_currentLevelConfig.CurrentValue.IncomePerClick));

            // Клик — хот-пасс: ToString + раскраска + интерполяция на каждое нажатие.
            if (!_logger.AreLogsEnabled)
            {
                return;
            }

            var clickCountText = _data.ClickCount.ToString().SetFeatureColor();
            _logger.Log($"Clicker clicked! Click count: {clickCountText}.");
        }

        public bool TryUpgrade()
        {
            if (!_canUpgrade.CurrentValue)
            {
                _logger.Log($"Can't upgrade clicker level. Max level reached: {_data.Level.ToString().SetFeatureColor()}.");
                return false;
            }

            if (!_inventory.Remove(new ItemOperation(_currentLevelConfig.CurrentValue.UpgradeCost)))
            {
                return false;
            }

            _data.Upgrade();
            _level.Value = ClampLevel(_data.Level);
            _logger.Log($"Clicker level upgraded! Clicker level: {_data.Level.ToString().SetFeatureColor()}.");
            return true;
        }

        public void Dispose()
        {
            _canUpgrade.Dispose();
            _currentLevelConfig.Dispose();
            _level.Dispose();
        }
    }
}
