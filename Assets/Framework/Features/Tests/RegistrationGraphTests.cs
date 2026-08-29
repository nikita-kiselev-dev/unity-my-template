using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Foundation.Analytics;
using Framework.Foundation.Audio;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Extensions;
using Framework.Foundation.Initialization.Registrators.Ads;
using Framework.Foundation.Initialization.Registrators.Data;
using Framework.Foundation.Initialization.Registrators.LiveOps;
using Framework.Foundation.Localization;
using Framework.Foundation.UI.LoadingCurtain.Controller;
using NUnit.Framework;
using R3;
using VContainer;

namespace Framework.Features.Tests
{
    /// <summary>
    /// Статическая валидация рантайм-графа DI: собираем те же регистрации, что RootScope,
    /// BootstrapScope и регистраторы, и проверяем, что каждую [Inject]-зависимость
    /// зарегистрированного типа кто-то закрывает. Тест живёт в Game.Tests, потому что
    /// рантайм-граф — это Core и Game вместе.
    /// </summary>
    public class RegistrationGraphTests
    {
        private static readonly Assembly _core = typeof(LifecycleEntity).Assembly;
        private static readonly Assembly _game = typeof(MainMenu.MainMenuCore).Assembly;

        /// Точки расширения: реализации живут в Integrations/ и подключаются проектом,
        /// в шаблоне такая коллекция законно пуста. Новый элемент здесь — сознательное решение.
        private static readonly Type[] _optionalCollectionElements =
        {
            typeof(IAnalyticsService),
            typeof(ILocaleSource),
        };

        [Test]
        public void Graph_ResolvesEveryInjectedDependency()
        {
            var registrations = BuildRegistrations(CreateRuntimeBuilder());

            var unresolved = FindUnresolved(registrations);

            Assert.IsEmpty(unresolved, "Незакрытые зависимости:\n" + string.Join("\n", unresolved));
        }

        [Test]
        public void Graph_RegistersLifecycleEntities_AsLifecycleEntity()
        {
            var registrations = BuildRegistrations(CreateRuntimeBuilder());

            var entities = registrations
                .Where(registration => typeof(LifecycleEntity).IsAssignableFrom(registration.ImplementationType))
                .ToArray();

            Assert.IsNotEmpty(entities);
            foreach (var registration in entities)
            {
                Assert.Contains(typeof(LifecycleEntity), registration.InterfaceTypes.ToArray(),
                    $"{registration.ImplementationType.Name} не резолвится как LifecycleEntity — SceneStarter его не увидит.");
            }
        }

        [Test]
        public void Graph_ResolvesLifecycleEntityList_ForSceneStarter()
        {
            var registrations = BuildRegistrations(CreateRuntimeBuilder());

            Assert.IsTrue(IsResolvable(typeof(IReadOnlyList<LifecycleEntity>), CollectRegisteredTypes(registrations)));
        }

        [Test]
        public void Graph_AnalyzesTypesCreatedByContainer()
        {
            var registrations = BuildRegistrations(CreateRuntimeBuilder());

            var analyzed = registrations
                .Where(IsConstructedByContainer)
                .Select(registration => registration.ImplementationType)
                .ToArray();

            Assert.Contains(typeof(SceneStarter), analyzed);
            Assert.Contains(typeof(Items.Inventory), analyzed);
        }

        /// Captive dependency: Singleton в root резолвит Scoped-регистрацию через root-контейнер и
        /// навсегда держит инстанс, отдельный от сценового. Для сервиса с кэшем это два кэша.
        [Test]
        public void Graph_DoesNotCaptureScopedDependencies_InRootSingletons()
        {
            var captured = FindCaptiveDependencies(BuildRegistrations(CreateRootBuilder()));

            Assert.IsEmpty(captured, "Singleton держит Scoped-зависимость:\n" + string.Join("\n", captured));
        }

        [Test]
        public void Validation_ReportsCaptiveDependency_WhenSingletonDependsOnScoped()
        {
            var builder = new ContainerBuilder();
            builder.Register(typeof(ScopedService), Lifetime.Scoped).AsSelf();
            builder.Register(typeof(SingletonConsumer), Lifetime.Singleton).AsSelf();

            var captured = FindCaptiveDependencies(BuildRegistrations(builder));

            Assert.AreEqual(1, captured.Length);
            Assert.That(captured[0], Does.Contain(nameof(ScopedService)));
        }

        [Test]
        public void Validation_ReportsDependency_WhenNothingRegistersIt()
        {
            var builder = new ContainerBuilder();
            builder.Register(typeof(BrokenConsumer), Lifetime.Singleton).AsSelf();

            var unresolved = FindUnresolved(BuildRegistrations(builder));

            Assert.AreEqual(1, unresolved.Length);
            Assert.That(unresolved[0], Does.Contain(nameof(IUnregisteredService)));
        }

        [Test]
        public void Validation_IgnoresRegistration_WhenContainerDoesNotCreateInstance()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(new PreBuiltConsumer(null));

            var unresolved = FindUnresolved(BuildRegistrations(builder));

            Assert.IsEmpty(unresolved);
        }

        /// Повторяет регистрации RootScope/BootstrapScope без Unity: компоненты из префаба и сцены
        /// регистрируются инстансом, но для графа важен только их тип.
        private static ContainerBuilder CreateRuntimeBuilder()
        {
            var builder = CreateRootBuilder();

            builder.Register(typeof(LoadingCurtainController), Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register(typeof(GameBootstrapper), Lifetime.Singleton).AsSelf();

            // BootstrapScope/SceneScope делают это через RegisterEntryPoint — для графа это обычная регистрация.
            builder.Register(typeof(SceneStarter), Lifetime.Singleton).AsImplementedInterfaces();

            return builder;
        }

        /// Только то, что регистрирует RootScope: эти типы живут в корневом контейнере, поэтому
        /// именно для них Scoped-зависимость означает captive dependency.
        private static ContainerBuilder CreateRootBuilder()
        {
            var builder = new ContainerBuilder();

            LiveOpsScopeRegistrator.Configure(builder);
            DataScopeRegistrator.Configure(builder);
            AdsScopeRegistrator.Configure(builder);

            var scan = AutoTypeScanner.Scan(new[] { _core, _game });
            builder.RegisterAutoTypes(scan);
            builder.RegisterConfigs(scan);
            builder.RegisterInstance<TimeProvider>(ObservableSystem.DefaultTimeProvider);

            builder.Register(typeof(AudioController), Lifetime.Singleton).AsImplementedInterfaces();

            return builder;
        }

        private static Registration[] BuildRegistrations(ContainerBuilder builder)
        {
            var registrations = new Registration[builder.Count];

            for (var i = 0; i < builder.Count; i++)
            {
                registrations[i] = builder[i].Build();
            }

            return registrations;
        }

        private static HashSet<Type> CollectRegisteredTypes(IReadOnlyList<Registration> registrations)
        {
            // Контейнер всегда отдаёт сам себя, регистрации для этого нет.
            var registered = new HashSet<Type> { typeof(IObjectResolver) };

            foreach (var registration in registrations)
            {
                if (registration.InterfaceTypes == null)
                {
                    registered.Add(registration.ImplementationType);
                    continue;
                }

                foreach (var interfaceType in registration.InterfaceTypes)
                {
                    registered.Add(interfaceType);
                }
            }

            return registered;
        }

        private static string[] FindCaptiveDependencies(IReadOnlyList<Registration> registrations)
        {
            var scoped = new HashSet<Type>();

            foreach (var registration in registrations.Where(registration => registration.Lifetime == Lifetime.Scoped))
            {
                scoped.Add(registration.ImplementationType);

                if (registration.InterfaceTypes == null)
                {
                    continue;
                }

                foreach (var interfaceType in registration.InterfaceTypes)
                {
                    scoped.Add(interfaceType);
                }
            }

            var captured = new List<string>();

            foreach (var registration in registrations)
            {
                if (registration.Lifetime != Lifetime.Singleton || !IsConstructedByContainer(registration))
                {
                    continue;
                }

                foreach (var (member, dependency) in GetDependencies(registration.ImplementationType))
                {
                    if (IsScoped(dependency, scoped))
                    {
                        captured.Add($"{registration.ImplementationType.Name}.{member} -> {dependency.Name} (Scoped)");
                    }
                }
            }

            return captured.ToArray();
        }

        private static bool IsScoped(Type dependency, HashSet<Type> scoped)
        {
            if (scoped.Contains(dependency))
            {
                return true;
            }

            if (dependency.IsArray)
            {
                return IsScoped(dependency.GetElementType(), scoped);
            }

            if (!dependency.IsConstructedGenericType)
            {
                return false;
            }

            var openGeneric = dependency.GetGenericTypeDefinition();

            if (openGeneric != typeof(IEnumerable<>) && openGeneric != typeof(IReadOnlyList<>))
            {
                return false;
            }

            return IsScoped(dependency.GetGenericArguments()[0], scoped);
        }

        private static string[] FindUnresolved(IReadOnlyList<Registration> registrations)
        {
            var registered = CollectRegisteredTypes(registrations);
            var unresolved = new List<string>();

            foreach (var registration in registrations)
            {
                if (!IsConstructedByContainer(registration))
                {
                    continue;
                }

                foreach (var (member, dependency) in GetDependencies(registration.ImplementationType))
                {
                    if (!IsResolvable(dependency, registered))
                    {
                        unresolved.Add($"{registration.ImplementationType.Name}.{member} -> {dependency}");
                    }
                }
            }

            return unresolved.ToArray();
        }

        /// Инжектит контейнер только в то, что создаёт сам: за это отвечает дефолтный
        /// `InstanceProvider` из `RegistrationBuilder.Build()`. Готовые инстансы
        /// (`RegisterInstance`), фабрики (`Func`) и config (инстанс отдаёт `IConfigProvider`) он
        /// возвращает как есть — их конструкторы и `[Inject]`-члены к графу отношения не имеют.
        /// Пример: в плеере `RootScope` регистрирует инстанс `TimeProvider`, которым R3 подставляет
        /// `UnityTimeProvider` с параметрами конструктора `(FrameProvider, TimeKind)`.
        /// Классы провайдеров `internal`, поэтому сверяемся по имени типа; что фильтр не выкосил
        /// всё разом, проверяет `Graph_AnalyzesTypesCreatedByContainer`.
        private static bool IsConstructedByContainer(Registration registration)
        {
            return registration.Provider.GetType().Name == "InstanceProvider";
        }

        /// Повторяет правила VContainer.Internal.TypeAnalyzer: [Inject]-конструктор
        /// (иначе конструктор с наибольшим числом параметров), [Inject]-поля, свойства и методы
        /// по всей иерархии.
        private static IEnumerable<(string member, Type type)> GetDependencies(Type implementationType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (var parameter in GetInjectConstructorParameters(implementationType, flags))
            {
                yield return ($"ctor({parameter.Name})", parameter.ParameterType);
            }

            for (var type = implementationType; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags).Where(IsInjected))
                {
                    yield return (field.Name, field.FieldType);
                }

                foreach (var property in type.GetProperties(flags).Where(IsInjected))
                {
                    yield return (property.Name, property.PropertyType);
                }

                foreach (var method in type.GetMethods(flags).Where(IsInjected))
                {
                    foreach (var parameter in method.GetParameters())
                    {
                        yield return ($"{method.Name}({parameter.Name})", parameter.ParameterType);
                    }
                }
            }
        }

        private static ParameterInfo[] GetInjectConstructorParameters(Type implementationType, BindingFlags flags)
        {
            // Компоненты создаёт Unity, конструктор контейнер не вызывает.
            if (typeof(UnityEngine.Object).IsAssignableFrom(implementationType))
            {
                return Array.Empty<ParameterInfo>();
            }

            var constructors = implementationType.GetConstructors(flags);
            var annotated = constructors.FirstOrDefault(IsInjected);

            if (annotated != null)
            {
                return annotated.GetParameters();
            }

            return constructors
                .OrderByDescending(constructor => constructor.GetParameters().Length)
                .FirstOrDefault()
                ?.GetParameters() ?? Array.Empty<ParameterInfo>();
        }

        private static bool IsInjected(MemberInfo member)
        {
            return member.IsDefined(typeof(InjectAttribute), inherit: false);
        }

        private static bool IsResolvable(Type dependency, HashSet<Type> registered)
        {
            if (registered.Contains(dependency))
            {
                return true;
            }

            // Компоненты сцены и ScriptableObject-конфиги приходят из префабов (RootScope, RootGameScope),
            // статически их набор не известен.
            if (typeof(UnityEngine.Object).IsAssignableFrom(dependency))
            {
                return true;
            }

            if (dependency.IsArray)
            {
                return IsResolvable(dependency.GetElementType(), registered);
            }

            if (!dependency.IsConstructedGenericType)
            {
                return false;
            }

            var openGeneric = dependency.GetGenericTypeDefinition();

            // Коллекцию VContainer соберёт всегда, но пустая коллекция — это молчаливое ничто,
            // поэтому требуем зарегистрированный элемент.
            if (openGeneric == typeof(IEnumerable<>) || openGeneric == typeof(IReadOnlyList<>))
            {
                var elementType = dependency.GetGenericArguments()[0];

                return _optionalCollectionElements.Contains(elementType) || IsResolvable(elementType, registered);
            }

            return false;
        }

        private interface IUnregisteredService
        {
        }

        private sealed class ScopedService
        {
        }

        private sealed class SingletonConsumer
        {
            [Inject] private readonly ScopedService _service;
        }

        private sealed class BrokenConsumer
        {
            [Inject] private readonly IUnregisteredService _service;
        }

        /// Готовый инстанс: контейнер его не создаёт и не инжектит, поэтому ни конструктор,
        /// ни [Inject]-поле в графе не участвуют.
        private sealed class PreBuiltConsumer
        {
            [Inject] private readonly IUnregisteredService _injected;

            public PreBuiltConsumer(IUnregisteredService service)
            {
                _injected = service;
            }
        }
    }
}
