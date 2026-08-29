using System;
using System.Collections.Generic;
using Framework.Foundation.Initialization.Scopes;
using UnityEngine;
using VContainer;

namespace Framework.Features.Initialization.Scopes
{
    public class RootGameScope : RootScope
    {
        [SerializeField] private ScriptableObject[] m_Configs;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            var registeredTypes = new HashSet<Type>();
            foreach (var config in m_Configs)
            {
                // Конфиги резолвятся по конкретному типу — второй инстанс того же типа
                // молча перекрыл бы первый, это ошибка настройки префаба.
                if (!registeredTypes.Add(config.GetType()))
                {
                    throw new InvalidOperationException(
                        $"{nameof(RootGameScope)}: duplicate config type {config.GetType().Name} in {nameof(m_Configs)}.");
                }

                builder.RegisterInstance(config).As(config.GetType());
            }
        }
    }
}
