using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mutator
{
    // Файл, который мутировать бессмысленно: код в нём по конвенции проекта не покрывается тестами,
    // поэтому его выживший мутант — не дыра в ассертах, а шум измерения.
    internal sealed class ExcludedFile
    {
        public string Path;
        public string Reason;
        public string Type;
    }

    // Признак «файл не подлежит мутации» выводится семантикой, а не именем файла: GradientColor
    // наследует BaseMeshEffect и не ловится ни именем, ни путём. Компиляция берётся из того же
    // .rsp, которым собирается сборка мутанта, — набор ссылок и дефайнов обязан совпадать,
    // иначе разрешение базовых типов разъедется с реальной сборкой.
    internal static class TypeScanner
    {
        private const string UnityObject = "UnityEngine.Object";

        public static IReadOnlyList<ExcludedFile> Scan(string responseFilePath)
        {
            var response = ReadResponseFile(responseFilePath);

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: response.Defines);
            var trees = response.Sources
                .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
                .ToList();

            var compilation = CSharpCompilation.Create(
                "MutatorScan",
                trees,
                response.References.Select(reference => MetadataReference.CreateFromFile(reference)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Отсутствие UnityEngine.Object означает, что ссылки не доехали. Молчаливый ноль
            // исключений в этом случае неотличим от «в проекте нет Unity-типов»: измерение
            // выглядело бы работающим, а фильтр был бы выключен.
            var unityObject = compilation.GetTypeByMetadataName(UnityObject);
            if (unityObject == null)
            {
                throw new InvalidOperationException(
                    $"{UnityObject} не разрешается по ссылкам из {responseFilePath}: фильтр по Unity-типам выключился бы молча.");
            }

            var excluded = new List<ExcludedFile>();

            foreach (var tree in trees)
            {
                var model = compilation.GetSemanticModel(tree);
                // Вложенные типы не считаются: решение принимается по объемлющему типу.
                var declarations = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<BaseTypeDeclarationSyntax>()
                    .Where(declaration => !(declaration.Parent is BaseTypeDeclarationSyntax))
                    .ToList();

                if (declarations.Count == 0)
                {
                    continue;
                }

                var reasons = declarations
                    .Select(declaration => Classify(model, declaration, unityObject))
                    .ToList();

                // Файл исключается целиком и только если исключается каждый объявленный в нём тип:
                // единица мутации — файл, а не тип, и половинчатое решение прятало бы живую логику,
                // лежащую рядом с MonoBehaviour.
                if (reasons.Any(reason => reason == null))
                {
                    continue;
                }

                excluded.Add(new ExcludedFile
                {
                    Path = tree.FilePath,
                    Reason = reasons[0],
                    Type = declarations[0].Identifier.Text,
                });
            }

            return excluded.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // null — тип мутируется. Иначе — код причины для отчёта.
        private static string Classify(
            SemanticModel model,
            BaseTypeDeclarationSyntax declaration,
            INamedTypeSymbol unityObject)
        {
            // Composition root фичи по конвенции наименования: только сборка зависимостей,
            // проверять в нём нечего (см. Naming.md, суффикс Core).
            if (declaration.Identifier.Text.EndsWith("Core", StringComparison.Ordinal))
            {
                return "core-suffix";
            }

            var symbol = model.GetDeclaredSymbol(declaration);
            if (symbol == null)
            {
                return null;
            }

            for (var current = symbol.BaseType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, unityObject))
                {
                    return "unity-object";
                }

                // Неразрешённая база — молчаливое «не Unity-тип». Сообщаем, а не додумываем.
                if (current.TypeKind == TypeKind.Error)
                {
                    Console.Error.WriteLine(
                        $"предупреждение: базовый тип '{current.Name}' у {symbol.Name} не разрешён — принадлежность к {UnityObject} не проверена.");
                }
            }

            return null;
        }

        // --- Response-файл -----------------------------------------------------------------------

        private sealed class ResponseFile
        {
            public List<string> Sources = new List<string>();
            public List<string> References = new List<string>();
            public List<string> Defines = new List<string>();
        }

        private static ResponseFile ReadResponseFile(string path)
        {
            var response = new ResponseFile();

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("-r:", StringComparison.Ordinal))
                {
                    var reference = Unquote(line.Substring("-r:".Length));
                    if (File.Exists(reference))
                    {
                        response.References.Add(reference);
                    }

                    continue;
                }

                if (line.StartsWith("-define:", StringComparison.Ordinal))
                {
                    response.Defines.AddRange(Unquote(line.Substring("-define:".Length))
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(symbol => symbol.Trim())
                        .Where(symbol => symbol.Length > 0));
                    continue;
                }

                if (line.StartsWith("-", StringComparison.Ordinal) || line.StartsWith("@", StringComparison.Ordinal))
                {
                    continue;
                }

                var source = Unquote(line);
                if (File.Exists(source))
                {
                    response.Sources.Add(source);
                }
            }

            return response;
        }

        private static string Unquote(string value)
        {
            var trimmed = value.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"'
                ? trimmed.Substring(1, trimmed.Length - 2)
                : trimmed;
        }
    }
}
