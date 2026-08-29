using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AutoDecorators.Generator.Tests
{
    internal sealed class GeneratorRunResult
    {
        public ImmutableArray<Diagnostic> GeneratorDiagnostics;
        public IReadOnlyList<string> GeneratedSources;
        public IReadOnlyList<Diagnostic> CompilationErrors;

        public string[] DiagnosticIds => GeneratorDiagnostics.Select(diagnostic => diagnostic.Id).ToArray();

        public string SingleGeneratedSource => GeneratedSources.Count == 1
            ? GeneratedSources[0]
            : throw new InvalidOperationException(
                $"Ожидался один сгенерированный файл, получено {GeneratedSources.Count}.");
    }

    internal static class GeneratorHarness
    {
        private static readonly ImmutableArray<MetadataReference> _references = LoadRuntimeReferences();

        public static GeneratorRunResult Run(string source)
        {
            var compilation = CSharpCompilation.Create(
                "AutoDecoratorsGeneratorTests",
                new[]
                {
                    CSharpSyntaxTree.ParseText(FrameworkStubs.Source),
                    CSharpSyntaxTree.ParseText(source),
                },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(new AutoDecoratorsGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var generatorDiagnostics);

            var generated = updated.SyntaxTrees
                .Skip(compilation.SyntaxTrees.Length)
                .Select(tree => Normalize(tree.ToString()))
                .ToArray();

            return new GeneratorRunResult
            {
                GeneratorDiagnostics = generatorDiagnostics,
                GeneratedSources = generated,
                CompilationErrors = updated
                    .GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .ToArray(),
            };
        }

        /// Генератор пишет строки через AppendLine, то есть Environment.NewLine: на Linux-раннере
        /// CI переносы отличаются от Windows, поэтому снапшоты сравниваем нормализованными.
        public static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Trim();
        }

        private static ImmutableArray<MetadataReference> LoadRuntimeReferences()
        {
            var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");

            return trusted
                .Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                // Один и тот же файл может прийти дважды (рантайм + каталог теста) — Roslyn
                // ругается на две ссылки с одинаковым identity.
                .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(group => (MetadataReference)MetadataReference.CreateFromFile(group.First()))
                .ToImmutableArray();
        }
    }
}
