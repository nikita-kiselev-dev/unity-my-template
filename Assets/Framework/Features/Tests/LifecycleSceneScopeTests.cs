using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Framework.Foundation.Initialization;
using Framework.Foundation.Scenes;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    /// Сцена, на которую объявлена хотя бы одна entity, но без scope-префаба, не выполняет
    /// ни одной фазы — молча, без ошибки и варнинга. Так на CoreScene не подключался звук
    /// кнопок и не поднимался оверлей статусов. Этот тест ловит расхождение до запуска.
    public class LifecycleSceneScopeTests
    {
        private const string ScopesRelativePath = "Assets/Framework/Foundation/Initialization/Content/Scopes";
        private const string ScopeSuffix = "Scope";
        private const string PrefabExtension = ".prefab";

        [Test]
        public void EveryLifecycleScene_HasScopePrefab()
        {
            var scopesDirectory = FindDirectory(ScopesRelativePath);

            if (scopesDirectory == null)
            {
                Assert.Ignore($"Не найден каталог {ScopesRelativePath} — сверка невозможна.");
            }

            var existingScopes = new HashSet<string>(
                Directory.EnumerateFiles(scopesDirectory, "*" + PrefabExtension)
                    .Select(Path.GetFileNameWithoutExtension),
                StringComparer.Ordinal);

            var missing = CollectLifecycleScenes()
                .Select(scene => scene.ScopeName)
                .Where(scopeName => !existingScopes.Contains(scopeName))
                .Distinct()
                .ToArray();

            Assert.IsEmpty(
                missing,
                $"Сцена объявлена в [LifecycleOrder], но scope-префаба нет: {string.Join(", ", missing)}");
        }

        /// Имя сцены в атрибуте берётся из SceneConstants.Scenes — литерал мимо констант
        /// не поймал бы ни компилятор, ни тест выше.
        [Test]
        public void EveryLifecycleScene_IsDeclaredInSceneConstants()
        {
            var known = SceneNamesByConstant();

            var unknown = CollectLifecycleScenes()
                .Where(scene => !known.ContainsValue(scene.SceneName))
                .Select(scene => $"{scene.Owner}: '{scene.SceneName}'")
                .Distinct()
                .ToArray();

            Assert.IsEmpty(
                unknown,
                $"[LifecycleOrder] ссылается на сцену вне SceneConstants.Scenes: {string.Join(", ", unknown)}");
        }

        private static List<(string SceneName, string ScopeName, string Owner)> CollectLifecycleScenes()
        {
            var scopeNameByScene = SceneNamesByConstant()
                .ToDictionary(entry => entry.Value, entry => entry.Key + ScopeSuffix, StringComparer.Ordinal);

            var scenes = GetFrameworkTypes()
                .SelectMany(type => type
                    .GetCustomAttributes<LifecycleOrderAttribute>()
                    .Select(attribute => (
                        SceneName: attribute.SceneScopeName,
                        ScopeName: scopeNameByScene.TryGetValue(attribute.SceneScopeName, out var scopeName)
                            ? scopeName
                            : null,
                        Owner: type.Name)))
                .Where(scene => scene.ScopeName != null)
                .ToList();

            // Пустой результат означает, что сломался сам скан, а не что сущностей нет.
            Assert.IsNotEmpty(scenes, "Не найдено ни одного типа с [LifecycleOrder] — скан сломан.");

            return scenes;
        }

        /// Ключ — имя константы (Bootstrap, Start, Core, Meta), значение — имя сцены.
        /// Префаб scope называется <имя константы> + "Scope", поэтому руками карту не держим.
        private static Dictionary<string, string> SceneNamesByConstant()
        {
            return typeof(SceneConstants.Scenes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue(), StringComparer.Ordinal);
        }

        private static IEnumerable<Type> GetFrameworkTypes()
        {
            var foundationAssembly = typeof(LifecycleOrderAttribute).Assembly;
            var foundationName = foundationAssembly.GetName().Name;

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly == foundationAssembly ||
                                   assembly.GetReferencedAssemblies().Any(reference => reference.Name == foundationName))
                .SelectMany(assembly => assembly.GetTypes());
        }

        // Корень проекта ищем от расположения сборки: работает и в Unity
        // (Library/ScriptAssemblies), и в быстром прогоне вне редактора (Temp/FastTests).
        private static string FindDirectory(string relativePath)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(LifecycleSceneScopeTests).Assembly.Location));

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
