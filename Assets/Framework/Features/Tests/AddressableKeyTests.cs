using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Framework.Foundation.Initialization.Decorators.AutoView;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    /// Ключ ассета живёт в трёх местах независимо: константа фичи, атрибут [AutoWindow]/[AutoPopup]
    /// и запись в Addressables. Генератор проверяет только первые два, отсутствующая запись
    /// падает в рантайме в фазе Load — эти тесты ловят рассинхрон до запуска игры.
    /// Читаем авторинг Addressables (группы в Assets/), а не собранный каталог: каталога может
    /// не быть вовсе, а ошибка живёт именно в авторинге.
    public class AddressableKeyTests
    {
        private const string GroupsRelativePath = "Assets/AddressableAssetsData/AssetGroups";
        private const string AddressField = "m_Address:";

        /// Вложенные классы, которые по конвенции держат ключи ассетов. Список опт-ин: рядом живут
        /// классы с ключами локализации, именами аналитики и форматами — адресами они не являются.
        /// Появился новый держатель ключей — добавить сюда, иначе он выпадет из проверки.
        private static readonly string[] KeyHolderNames = { "Prefabs", "Configs", "Canvases", "Sounds", "Music", "Atlases" };

        private const string KeyHolderSuffix = "Keys";

        [Test]
        public void AutoViewKeys_HaveAddressablesEntry()
        {
            var addresses = ReadAddressableAddresses();
            var keys = CollectAutoViewKeys();

            var missing = keys
                .Where(key => !addresses.Contains(key.Key))
                .Select(key => $"{key.Owner}: '{key.Key}'")
                .ToArray();

            Assert.IsEmpty(missing, $"Нет записи в Addressables для ключей view: {string.Join(", ", missing)}");
        }

        [Test]
        public void AutoViewKeys_AreUniqueAcrossTypes()
        {
            var duplicates = CollectAutoViewKeys()
                .GroupBy(key => key.Key)
                .Where(group => group.Select(key => key.Owner).Distinct().Count() > 1)
                .Select(group => $"'{group.Key}' — {string.Join(", ", group.Select(key => key.Owner).Distinct())}")
                .ToArray();

            Assert.IsEmpty(duplicates, $"Один ключ view на несколько типов: {string.Join("; ", duplicates)}");
        }

        /// [AutoWindow]/[AutoPopup] покрывают только поля view. Ключи, которые код передаёт в
        /// IAssetProvider строкой (звуки, музыка, канвасы, конфиги, префабы дней DailyBonus),
        /// живут в константах и до этого теста не сверялись ни с чем.
        [Test]
        public void AssetKeyConstants_HaveAddressablesEntry()
        {
            var addresses = ReadAddressableAddresses();
            var constants = CollectAssetKeyConstants();

            var missing = constants
                .Where(constant => !addresses.Contains(constant.Key))
                .Select(constant => $"{constant.Owner} = '{constant.Key}'")
                .ToArray();

            Assert.IsEmpty(missing, $"Нет записи в Addressables для ключей ассетов: {string.Join(", ", missing)}");
        }

        private static List<(string Key, string Owner)> CollectAutoViewKeys()
        {
            var keys = GetFrameworkTypes()
                .SelectMany(type => type
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Select(field => (Key: GetViewKey(field), Owner: type.Name))
                    .Where(entry => entry.Key != null))
                .ToList();

            // Пустой результат означает, что сломался сам скан, а не что ключей нет:
            // в шаблоне есть окна с [AutoWindow]/[AutoPopup].
            Assert.IsNotEmpty(keys, "Не найдено ни одного ключа [AutoWindow]/[AutoPopup] — скан сборок сломан.");

            return keys;
        }

        private static List<(string Key, string Owner)> CollectAssetKeyConstants()
        {
            var constants = GetFrameworkTypes()
                .Where(IsKeyHolder)
                .SelectMany(type => type
                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                    .Select(field => (Key: (string)field.GetRawConstantValue(), Owner: $"{GetOwnerName(type)}.{field.Name}")))
                .ToList();

            Assert.IsNotEmpty(constants, "Не найдено ни одной константы-ключа ассета — скан сборок сломан.");

            return constants;
        }

        private static bool IsKeyHolder(Type type)
        {
            return KeyHolderNames.Contains(type.Name) || type.Name.EndsWith(KeyHolderSuffix, StringComparison.Ordinal);
        }

        private static string GetOwnerName(Type type)
        {
            return type.IsNested ? $"{type.DeclaringType.Name}.{type.Name}" : type.Name;
        }

        private static IEnumerable<Type> GetFrameworkTypes()
        {
            var foundationAssembly = typeof(AutoWindowAttribute).Assembly;
            var foundationName = foundationAssembly.GetName().Name;

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly == foundationAssembly ||
                                   assembly.GetReferencedAssemblies().Any(reference => reference.Name == foundationName))
                .SelectMany(assembly => assembly.GetTypes());
        }

        private static string GetViewKey(FieldInfo field)
        {
            return field.GetCustomAttribute<AutoWindowAttribute>()?.ViewKey ?? field.GetCustomAttribute<AutoPopupAttribute>()?.ViewKey;
        }

        private static HashSet<string> ReadAddressableAddresses()
        {
            var groupsDirectory = FindGroupsDirectory();

            if (groupsDirectory == null)
            {
                Assert.Ignore($"Не найден каталог {GroupsRelativePath} — сверка с Addressables невозможна.");
            }

            var addresses = Directory.EnumerateFiles(groupsDirectory, "*.asset")
                .SelectMany(File.ReadAllLines)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(AddressField, StringComparison.Ordinal))
                .Select(line => line.Substring(AddressField.Length).Trim());

            return new HashSet<string>(addresses, StringComparer.Ordinal);
        }

        // Корень проекта ищем от расположения сборки: работает и в Unity
        // (Library/ScriptAssemblies), и в быстром прогоне вне редактора (Temp/FastTests).
        private static string FindGroupsDirectory()
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(AddressableKeyTests).Assembly.Location));

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, GroupsRelativePath.Replace('/', Path.DirectorySeparatorChar));

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
