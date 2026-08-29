using System;
using System.Collections.Generic;
using System.Reflection;
using Framework.Foundation.Configs;
using Framework.Foundation.SaveLoad;
using VContainer;
using ZLinq;

namespace Framework.Foundation.Initialization
{
    /// <summary>
    /// Ищет типы для автоматической регистрации: <see cref="AutoRegistrationAttribute"/>,
    /// конкретные наследники <see cref="SaveBlob"/> и <see cref="IConfig"/> с <see cref="ConfigKeyAttribute"/>.
    /// Чистая функция от списка сборок — источник сборок выбирает вызывающий
    /// (в рантайме это AppDomain, в тестах — явный набор).
    /// </summary>
    public static class AutoTypeScanner
    {
        private static readonly Type _lifecycleEntityType = typeof(LifecycleEntity);
        private static readonly Type _dataType = typeof(SaveBlob);

        public static AutoTypeScanResult Scan(IEnumerable<Assembly> assemblies)
        {
            var autoTypes = new List<AutoTypeEntry>();
            var configCandidates = new List<Type>();
            var scanned = new HashSet<Assembly>();
            var coreAssembly = _lifecycleEntityType.Assembly;
            var coreName = coreAssembly.GetName().Name;

            foreach (var assembly in assemblies)
            {
                if (!scanned.Add(assembly) || !ShouldScan(assembly, coreAssembly, coreName))
                {
                    continue;
                }

                foreach (var type in GetTypes(assembly))
                {
                    Collect(type, autoTypes, configCandidates);
                }
            }

            return new AutoTypeScanResult(autoTypes.ToArray(), ConfigTypeScanner.Scan(configCandidates));
        }

        private static bool ShouldScan(Assembly assembly, Assembly coreAssembly, string coreName)
        {
            // Наследник LifecycleEntity/SaveBlob и тип с [AutoRegistration] физически не могут
            // жить в сборке без прямой ссылки на Core — пропускаем всё остальное
            // (Unity, сторонние плагины и т.п.).
            if (assembly != coreAssembly && !ReferencesAssembly(assembly, coreName))
            {
                return false;
            }

            // Тестовые сборки (Core.Tests/Game.Tests) ссылаются на Core и в редакторе
            // загружены в AppDomain. Их тестовые SaveBlob/LifecycleEntity/[AutoRegistration]-типы
            // не должны попадать в рантайм-контейнер — фильтруем по ссылке на NUnit.
            return !ReferencesAssembly(assembly, "nunit.framework");
        }

        private static void Collect(Type type, List<AutoTypeEntry> autoTypes, List<Type> configCandidates)
        {
            if (type == null || type.IsAbstract)
            {
                return;
            }

            if (type.IsDefined(typeof(ConfigKeyAttribute), inherit: false))
            {
                configCandidates.Add(type);
                return;
            }

            if (_dataType.IsAssignableFrom(type))
            {
                autoTypes.Add(new AutoTypeEntry(type, Lifetime.Singleton, AutoTypeKind.SaveBlob));
                return;
            }

            var attribute = type.GetCustomAttribute<AutoRegistrationAttribute>(inherit: false);
            if (attribute == null)
            {
                return;
            }

            var kind = _lifecycleEntityType.IsAssignableFrom(type)
                ? AutoTypeKind.LifecycleEntity
                : AutoTypeKind.Service;

            autoTypes.Add(new AutoTypeEntry(type, attribute.Lifetime, kind));
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.AsValueEnumerable().Where(type => type != null).ToArray();
            }
        }

        private static bool ReferencesAssembly(Assembly assembly, string targetName)
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name == targetName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
