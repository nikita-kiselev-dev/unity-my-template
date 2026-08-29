using System;
using Framework.Foundation.Configs;
using VContainer;

namespace Framework.Foundation.Initialization.Extensions
{
    /// Своя регистрация вместо generic-фабрики: типы config известны только в рантайме,
    /// а MakeGenericMethod на AOT-платформах ненадёжен.
    internal sealed class ConfigRegistrationBuilder : RegistrationBuilder
    {
        public ConfigRegistrationBuilder(Type configType) : base(configType, Lifetime.Singleton)
        {
        }

        public override Registration Build()
        {
            return new Registration(
                ImplementationType,
                Lifetime,
                InterfaceTypes,
                new ConfigInstanceProvider(ImplementationType));
        }

        private sealed class ConfigInstanceProvider : IInstanceProvider
        {
            private readonly Type _configType;

            public ConfigInstanceProvider(Type configType)
            {
                _configType = configType;
            }

            public object SpawnInstance(IObjectResolver resolver)
            {
                return resolver.Resolve<IConfigProvider>().Get(_configType);
            }
        }
    }
}
