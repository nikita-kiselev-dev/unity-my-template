using System;
using System.Collections.Generic;
using System.Reflection;
using Framework.Foundation.Configs;
using Framework.Foundation.Logger;
using VContainer;

namespace Framework.Foundation.Initialization
{
    /// Решает, нужна ли сущность в этом запуске: конъюнкция IsEnabled всех инжектируемых IConfig и
    /// IConditionalEntity.ShouldRun(). Считается до фаз, потому что config, серверное время и сейв
    /// к этому моменту уже готовы.
    public static class LifecycleGate
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, FieldInfo[]> _fieldCache = new();
        private static readonly Type _configType = typeof(IConfig);
        private static readonly Type _injectType = typeof(InjectAttribute);

        /// logger нужен только чтобы объяснить отказ: включённые сущности не логируются,
        /// иначе строка на каждую сущность каждой сцены — шум без пользы.
        public static void Apply(LifecycleEntity entity, ILogChannel logger = null)
        {
            if (!HasGate(entity))
            {
                return;
            }

            var disabledConfig = FindDisabledConfig(entity);
            var isEnabled = disabledConfig == null && IsEnabledByCondition(entity);
            entity.Status.SetEnabled(isEnabled);

            if (isEnabled)
            {
                return;
            }

            // Интерполяция и сам разбор причины считаются на стороне вызывающего, поэтому
            // guard стоит до сборки строки, а не внутри Log.
            if (logger == null || !logger.AreLogsEnabled)
            {
                return;
            }

            var reason = disabledConfig == null
                ? $"{nameof(IConditionalEntity)}.{nameof(IConditionalEntity.ShouldRun)}()"
                : $"{disabledConfig.Name}(IsEnabled=false)";

            logger.Log($"{entity.GetType().Name}: disabled by {reason}");
        }

        /// Гейтить по IsEnabled можно только сущности с источником решения: у остальных
        /// EntityStatus.IsEnabled равен false до того, как они включат себя в своём Init.
        public static bool IsDisabled(LifecycleEntity entity)
        {
            return HasGate(entity) && !entity.Status.IsEnabled;
        }

        private static bool HasGate(LifecycleEntity entity)
        {
            return entity is IConditionalEntity || GetConfigFields(entity.GetType()).Length > 0;
        }

        /// Возвращает тип первого выключенного конфига или null, если все включены:
        /// решение и его причина считаются одним проходом.
        private static Type FindDisabledConfig(LifecycleEntity entity)
        {
            foreach (var field in GetConfigFields(entity.GetType()))
            {
                var config = (IConfig)field.GetValue(entity);

                if (!config.IsEnabled)
                {
                    return config.GetType();
                }
            }

            return null;
        }

        // Конфиг сильнее условия: у выключенной конфигом фичи ShouldRun не спрашиваем, иначе её
        // побочные эффекты (мутация сейва, аналитика) выполнятся у отключённой фичи.
        private static bool IsEnabledByCondition(LifecycleEntity entity)
        {
            return entity is not IConditionalEntity conditional || conditional.ShouldRun();
        }

        private static FieldInfo[] GetConfigFields(Type entityType)
        {
            if (_fieldCache.TryGetValue(entityType, out var cached))
            {
                return cached;
            }

            var fields = new List<FieldInfo>();

            // DeclaredOnly + обход иерархии: иначе приватные поля базовых классов не видны.
            for (var type = entityType; type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(FieldFlags))
                {
                    if (_configType.IsAssignableFrom(field.FieldType) && field.IsDefined(_injectType, inherit: false))
                    {
                        fields.Add(field);
                    }
                }
            }

            var result = fields.ToArray();
            _fieldCache[entityType] = result;
            return result;
        }
    }
}
