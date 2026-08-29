using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AutoDecorators.Generator
{
    /// Генерит для классов с полями [AutoWindow]/[AutoPopup] partial-часть с реализацией
    /// IAutoViewHost — типизированные биндинги вместо рантайм-рефлексии.
    /// Открытие/закрытие view фича слушает явно через MonoView.Opened/Closed — генератор
    /// lifecycle-колбэков не эмитит.
    /// Для классов с [AutoLoggerAttribute] генерит свойство Logger и [Inject]-метод, получающий его
    /// из ILogChannelFactory; при StatusLogs = true дополнительно включает статус-логи LifecycleEntity.
    [Generator(LanguageNames.CSharp)]
    public sealed class AutoDecoratorsGenerator : IIncrementalGenerator
    {
        private const string AutoWindowAttributeName = "Framework.Foundation.Initialization.Decorators.AutoView.AutoWindowAttribute";
        private const string AutoPopupAttributeName = "Framework.Foundation.Initialization.Decorators.AutoView.AutoPopupAttribute";
        private const string AutoLoggerAttributeName = "Framework.Foundation.Initialization.Decorators.AutoLogger.AutoLoggerAttribute";
        private const string AutoViewNamespace = "global::Framework.Foundation.Initialization.Decorators.AutoView";
        private const string LogChannelFactoryFqn = "global::Framework.Foundation.Logger.ILogChannelFactory";
        private const string LogChannelFqn = "global::Framework.Foundation.Logger.ILogChannel";
        private const string LogCategoryFqn = "global::Framework.Foundation.Logger.LogCategory";
        private const string LifecycleEntityFqn = "Framework.Foundation.Initialization.LifecycleEntity";
        private const string WindowViewKindExpression = "global::Framework.Foundation.UI.Views.ViewKind.Window";
        private const string PopupViewKindExpression = "global::Framework.Foundation.UI.Views.ViewKind.Popup";
        private const string MonoViewFqn = "global::Framework.Foundation.UI.Views.MonoView";
        private const string ConfigFqn = "global::Framework.Foundation.Configs.IConfig";

        private static readonly DiagnosticDescriptor NotPartialError = new DiagnosticDescriptor(
            id: "ADG001",
            title: "Type must be partial",
            messageFormat: "Type '{0}' uses [AutoWindow]/[AutoPopup]/[AutoLogger] and must be declared partial",
            category: "AutoDecorators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor StatusLogsOnNonLifecycleEntityError = new DiagnosticDescriptor(
            id: "ADG002",
            title: "StatusLogs requires LifecycleEntity",
            messageFormat: "Type '{0}' sets [AutoLogger(StatusLogs = true)] but does not inherit LifecycleEntity",
            category: "AutoDecorators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor DuplicateViewKeyError = new DiagnosticDescriptor(
            id: "ADG003",
            title: "Duplicate view key",
            messageFormat: "Type '{0}' declares several [AutoWindow]/[AutoPopup] fields with the same view key {1}",
            category: "AutoDecorators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor DuplicateViewKeyAcrossTypesError = new DiagnosticDescriptor(
            id: "ADG004",
            title: "View key is not unique",
            messageFormat: "View key {0} is declared by several types: {1}",
            category: "AutoDecorators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var windowFields = CreateFieldProvider(context, AutoWindowAttributeName, FieldKind.View, WindowViewKindExpression);
            var popupFields = CreateFieldProvider(context, AutoPopupAttributeName, FieldKind.View, PopupViewKindExpression);
            var loggerClasses = context.SyntaxProvider.ForAttributeWithMetadataName(
                AutoLoggerAttributeName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, _) => CreateLoggerModel(ctx));

            var allFields = windowFields.Collect()
                .Combine(popupFields.Collect())
                .Combine(loggerClasses.Collect());

            context.RegisterSourceOutput(
                allFields,
                static (spc, pair) => Emit(spc, pair.Left.Left
                    .AddRange(pair.Left.Right)
                    .AddRange(pair.Right)));
        }

        private static IncrementalValuesProvider<FieldModel> CreateFieldProvider(
            IncrementalGeneratorInitializationContext context,
            string attributeName,
            FieldKind kind,
            string viewTypeExpression)
        {
            return context.SyntaxProvider.ForAttributeWithMetadataName(
                attributeName,
                static (node, _) => node is VariableDeclaratorSyntax,
                (ctx, _) => CreateFieldModel(ctx, kind, viewTypeExpression));
        }

        private static FieldModel CreateFieldModel(GeneratorAttributeSyntaxContext ctx, FieldKind kind, string viewTypeExpression)
        {
            var fieldSymbol = (IFieldSymbol)ctx.TargetSymbol;
            var containingType = fieldSymbol.ContainingType;
            var attribute = ctx.Attributes[0];

            var key = SymbolDisplay.FormatLiteral((string)attribute.ConstructorArguments[0].Value, quote: true);

            var nonPartialType = FindNonPartialType(containingType);

            var model = new FieldModel
            {
                IsPartial = nonPartialType == null,
                NonPartialTypeName = nonPartialType,
                Location = ctx.TargetNode.GetLocation(),
                Kind = kind,
                FieldName = fieldSymbol.Name,
                FieldTypeFqn = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Key = key,
                ViewTypeExpression = viewTypeExpression,
            };
            FillTypeInfo(model, containingType);
            return model;
        }

        private static FieldModel CreateLoggerModel(GeneratorAttributeSyntaxContext ctx)
        {
            var typeSymbol = (INamedTypeSymbol)ctx.TargetSymbol;
            var attribute = ctx.Attributes[0];

            var key = SymbolDisplay.FormatLiteral((string)attribute.ConstructorArguments[0].Value, quote: true);

            // Опциональный параметр атрибута Roslyn заполняет значением по умолчанию.
            var logEntityType = System.Convert.ToInt32(attribute.ConstructorArguments[1].Value);

            var statusLogs = attribute.NamedArguments
                .Where(pair => pair.Key == "StatusLogs")
                .Select(pair => (bool)pair.Value.Value)
                .FirstOrDefault();

            var nonPartialType = FindNonPartialType(typeSymbol);

            var model = new FieldModel
            {
                IsPartial = nonPartialType == null,
                NonPartialTypeName = nonPartialType,
                Location = ctx.TargetNode.GetLocation(),
                Kind = FieldKind.Logger,
                Key = key,
                LogCategory = logEntityType,
                StatusLogs = statusLogs,
                IsLifecycleEntity = InheritsLifecycleEntity(typeSymbol),
                IsSealed = typeSymbol.IsSealed,
            };
            FillTypeInfo(model, typeSymbol);
            return model;
        }

        /// FillTypeInfo печатает partial для всей цепочки вложенности, поэтому непартиальный
        /// outer даёт сырой CS0260 вместо ADG001. Возвращает имя первого такого типа.
        private static string FindNonPartialType(INamedTypeSymbol typeSymbol)
        {
            for (var type = typeSymbol; type != null; type = type.ContainingType)
            {
                if (!IsDeclaredPartial(type))
                {
                    return type.Name;
                }
            }

            return null;
        }

        private static bool IsDeclaredPartial(INamedTypeSymbol typeSymbol)
        {
            foreach (var reference in typeSymbol.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                    declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InheritsLifecycleEntity(INamedTypeSymbol typeSymbol)
        {
            for (var type = typeSymbol.BaseType; type != null; type = type.BaseType)
            {
                if (type.ToDisplayString() == LifecycleEntityFqn)
                {
                    return true;
                }
            }

            return false;
        }

        private static void FillTypeInfo(FieldModel model, INamedTypeSymbol containingType)
        {
            var typeChain = new List<string>();
            for (var type = containingType; type != null; type = type.ContainingType)
            {
                var keyword = type.TypeKind == TypeKind.Struct ? "struct" : "class";
                var typeParameters = type.TypeParameters.Length > 0
                    ? "<" + string.Join(", ", type.TypeParameters.Select(p => p.Name)) + ">"
                    : string.Empty;
                typeChain.Insert(0, $"partial {keyword} {type.Name}{typeParameters}");
            }

            model.Namespace = containingType.ContainingNamespace.IsGlobalNamespace
                ? null
                : containingType.ContainingNamespace.ToDisplayString();
            model.TypeChain = typeChain.ToArray();
            model.TypeFqn = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            model.TypeShortName = containingType.Name;
        }

        private static void Emit(SourceProductionContext context, ImmutableArray<FieldModel> fields)
        {
            ReportKeysSharedByTypes(context, fields);

            foreach (var typeGroup in fields.GroupBy(f => f.TypeFqn))
            {
                var first = typeGroup.First();

                var notPartial = typeGroup.FirstOrDefault(f => !f.IsPartial);
                if (notPartial != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        NotPartialError, notPartial.Location, notPartial.NonPartialTypeName));
                    continue;
                }

                var views = typeGroup.Where(f => f.Kind == FieldKind.View).ToArray();
                var logger = typeGroup.FirstOrDefault(f => f.Kind == FieldKind.Logger);

                if (logger != null && logger.StatusLogs && !logger.IsLifecycleEntity)
                {
                    context.ReportDiagnostic(Diagnostic.Create(StatusLogsOnNonLifecycleEntityError, logger.Location, logger.TypeShortName));
                    continue;
                }

                var duplicateKey = views.GroupBy(f => f.Key).FirstOrDefault(g => g.Count() > 1);
                if (duplicateKey != null)
                {
                    var duplicate = duplicateKey.Last();
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateViewKeyError, duplicate.Location, duplicate.TypeShortName, duplicate.Key));
                    continue;
                }

                var source = BuildSource(first, views, logger);
                var hintName = SanitizeHintName(first.TypeFqn);
                context.AddSource($"{hintName}.AutoDecorators.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        /// ViewRouter держит один словарь на все view: коллизия ключей двух разных фич падает
        /// только в рантайме, в фазе Init. Проверка глобальна по компиляции, поэтому дубль внутри
        /// одного класса остаётся за ADG003. Генерация не прерывается — иначе к ошибке о ключе
        /// добавились бы каскадные CS-ошибки об отсутствующем IAutoViewHost.
        private static void ReportKeysSharedByTypes(SourceProductionContext context, ImmutableArray<FieldModel> fields)
        {
            var keyGroups = fields
                .Where(f => f.Kind == FieldKind.View)
                .GroupBy(f => f.Key)
                .Where(g => g.Select(f => f.TypeFqn).Distinct().Count() > 1);

            foreach (var keyGroup in keyGroups)
            {
                var types = string.Join(", ", keyGroup
                    .Select(f => f.TypeShortName)
                    .Distinct()
                    .OrderBy(name => name, System.StringComparer.Ordinal));

                foreach (var field in keyGroup)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateViewKeyAcrossTypesError, field.Location, keyGroup.Key, types));
                }
            }
        }

        private static string BuildSource(FieldModel type, FieldModel[] views, FieldModel logger)
        {
            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated by AutoDecorators.Generator />");

            var indent = 0;
            if (type.Namespace != null)
            {
                builder.AppendLine($"namespace {type.Namespace}");
                builder.AppendLine("{");
                indent++;
            }

            for (var i = 0; i < type.TypeChain.Length; i++)
            {
                var isInnermost = i == type.TypeChain.Length - 1;
                var declaration = type.TypeChain[i];

                if (isInnermost && views.Length > 0)
                {
                    declaration += $" : {AutoViewNamespace}.IAutoViewHost";
                }

                AppendLine(builder, indent, declaration);
                AppendLine(builder, indent, "{");
                indent++;
            }

            if (views.Length > 0)
            {
                AppendLine(builder, indent, $"public {AutoViewNamespace}.AutoViewBinding[] GetAutoViewBindings() => new {AutoViewNamespace}.AutoViewBinding[]");
                AppendLine(builder, indent, "{");
                foreach (var view in views)
                {
                    AppendLine(builder, indent + 1,
                        $"new {AutoViewNamespace}.AutoViewBinding({view.Key}, {view.ViewTypeExpression}, view => this.{view.FieldName} = ({view.FieldTypeFqn})view),");
                }
                AppendLine(builder, indent, "};");
            }

            if (logger != null)
            {
                if (views.Length > 0)
                {
                    builder.AppendLine();
                }

                // protected в sealed-классе даёт CS0628, а private set у private-свойства — CS0273.
                var accessibility = logger.IsSealed ? "private" : "protected";
                var setter = logger.IsSealed ? "set;" : "private set;";
                var entityType = $"({LogCategoryFqn}){logger.LogCategory}";

                AppendLine(builder, indent, $"{accessibility} {LogChannelFqn} Logger {{ get; {setter} }}");
                builder.AppendLine();
                AppendLine(builder, indent, "[global::VContainer.Inject]");
                AppendLine(builder, indent, $"private void __InitAutoLogger({LogChannelFactoryFqn} logChannelFactory)");
                AppendLine(builder, indent, "{");
                AppendLine(builder, indent + 1, $"this.Logger = logChannelFactory.Get({logger.Key}, {entityType});");
                if (logger.StatusLogs)
                {
                    AppendLine(builder, indent + 1, $"this.EnableStatusLogs({entityType});");
                }
                AppendLine(builder, indent, "}");
            }

            while (indent > 0)
            {
                indent--;
                AppendLine(builder, indent, "}");
            }

            return builder.ToString();
        }

        private static void AppendLine(StringBuilder builder, int indent, string line)
        {
            builder.Append(' ', indent * 4).AppendLine(line);
        }

        private static string SanitizeHintName(string typeFqn)
        {
            var name = typeFqn.Replace("global::", string.Empty);
            var builder = new StringBuilder(name.Length);
            foreach (var symbol in name)
            {
                builder.Append(char.IsLetterOrDigit(symbol) || symbol == '.' ? symbol : '_');
            }

            return builder.ToString();
        }

        private enum FieldKind
        {
            View,
            Logger,
        }

        private sealed class FieldModel
        {
            public string Namespace;
            public string[] TypeChain;
            public string TypeFqn;
            public string TypeShortName;
            public bool IsPartial;
            public string NonPartialTypeName;
            public Location Location;
            public FieldKind Kind;
            public string FieldName;
            public string FieldTypeFqn;
            public string Key;
            public string ViewTypeExpression;
            public int LogCategory;
            public bool StatusLogs;
            public bool IsLifecycleEntity;
            public bool IsSealed;
        }
    }
}
