using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Mutator
{
    // Одна точечная правка исходника: подмена текста в диапазоне [SpanStart, SpanStart + SpanLength).
    // Мутант применяется сплайсом строки, а не переписыванием дерева — так мутированный файл
    // отличается от оригинала ровно в одном месте и остаётся читаемым в отчёте.
    internal sealed class Mutation
    {
        public int Index;
        public int Line;
        public int Column;
        public string Operator;
        public string Original;
        public string Mutated;
        public int SpanStart;
        public int SpanLength;
        public string Preview;
    }

    // Планировщик детерминирован: тот же исходник и те же строки дают тот же порядок мутантов.
    // На этом держится команда apply — она не читает план, а пересчитывает его и берёт мутанта по индексу.
    internal static class MutationPlanner
    {
        // Пары «оператор → чем заменяем». Обе стороны присутствуют: мутация должна менять поведение
        // и в коде, который уже написан через >= или !=.
        private static readonly Dictionary<SyntaxKind, (SyntaxKind Kind, string Text, string Name)> BinarySwaps =
            new Dictionary<SyntaxKind, (SyntaxKind, string, string)>
            {
                [SyntaxKind.GreaterThanToken] = (SyntaxKind.GreaterThanEqualsToken, ">=", "relational-boundary"),
                [SyntaxKind.GreaterThanEqualsToken] = (SyntaxKind.GreaterThanToken, ">", "relational-boundary"),
                [SyntaxKind.LessThanToken] = (SyntaxKind.LessThanEqualsToken, "<=", "relational-boundary"),
                [SyntaxKind.LessThanEqualsToken] = (SyntaxKind.LessThanToken, "<", "relational-boundary"),
                [SyntaxKind.EqualsEqualsToken] = (SyntaxKind.ExclamationEqualsToken, "!=", "equality"),
                [SyntaxKind.ExclamationEqualsToken] = (SyntaxKind.EqualsEqualsToken, "==", "equality"),
                [SyntaxKind.AmpersandAmpersandToken] = (SyntaxKind.BarBarToken, "||", "logical"),
                [SyntaxKind.BarBarToken] = (SyntaxKind.AmpersandAmpersandToken, "&&", "logical"),
                [SyntaxKind.PlusToken] = (SyntaxKind.MinusToken, "-", "arithmetic"),
                [SyntaxKind.MinusToken] = (SyntaxKind.PlusToken, "+", "arithmetic"),
            };

        public static IReadOnlyList<Mutation> Plan(string source, IReadOnlyCollection<int> changedLines)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();
            var text = tree.GetText();
            var scope = BuildScope(root, text, changedLines);

            var mutations = new List<Mutation>();

            foreach (var node in root.DescendantNodes())
            {
                if (!InScope(scope, node.Span))
                {
                    continue;
                }

                // Значения атрибутов — метаданные компиляции (порядок фаз, ключи Addressables):
                // мутация там ломает сборку или регистрацию, а не проверяемое поведение.
                if (node.Ancestors().Any(ancestor => ancestor is AttributeSyntax))
                {
                    continue;
                }

                switch (node)
                {
                    case BinaryExpressionSyntax binary:
                        AddBinary(binary, text, mutations);
                        break;

                    case LiteralExpressionSyntax literal:
                        AddBooleanLiteral(literal, text, mutations);
                        break;

                    case ReturnStatementSyntax returnStatement:
                        AddReturnDefault(returnStatement, text, mutations);
                        break;

                    case ExpressionStatementSyntax statement:
                        AddStatementRemoval(statement, text, mutations);
                        break;
                }
            }

            var ordered = mutations.OrderBy(mutation => mutation.SpanStart).ThenBy(mutation => mutation.Operator).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Index = i;
            }

            return ordered;
        }

        public static string Apply(string source, Mutation mutation)
        {
            return source.Substring(0, mutation.SpanStart)
                   + mutation.Mutated
                   + source.Substring(mutation.SpanStart + mutation.SpanLength);
        }

        // --- Операторы -----------------------------------------------------------------------

        private static void AddBinary(BinaryExpressionSyntax binary, SourceText text, List<Mutation> mutations)
        {
            var token = binary.OperatorToken;
            if (!BinarySwaps.TryGetValue(token.Kind(), out var swap))
            {
                return;
            }

            // Конкатенация строк через + при замене на - не компилируется: это не мутация,
            // а заведомо битый мутант, который только съел бы прогон.
            var arithmetic = token.IsKind(SyntaxKind.PlusToken) || token.IsKind(SyntaxKind.MinusToken);
            if (arithmetic && ContainsTextLiteral(binary))
            {
                return;
            }

            mutations.Add(Create(text, token.SpanStart, token.Span.Length, swap.Name, token.Text, swap.Text));
        }

        private static void AddBooleanLiteral(LiteralExpressionSyntax literal, SourceText text, List<Mutation> mutations)
        {
            if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                mutations.Add(Create(text, literal.SpanStart, literal.Span.Length, "boolean-literal", "true", "false"));
            }
            else if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                mutations.Add(Create(text, literal.SpanStart, literal.Span.Length, "boolean-literal", "false", "true"));
            }
        }

        private static void AddReturnDefault(ReturnStatementSyntax returnStatement, SourceText text, List<Mutation> mutations)
        {
            var expression = returnStatement.Expression;

            // ref-возврат нельзя заменить на default, а default → default не мутация.
            if (expression == null
                || expression is RefExpressionSyntax
                || expression is DefaultExpressionSyntax
                || expression.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                return;
            }

            // `return false` / `return 0` / `return null` → `return default` тождественно: значение
            // совпадает с default для своего типа. Такой мутант выживает всегда и на любом наборе
            // тестов, то есть не измеряет ничего, а место в отчёте занимает.
            if (IsDefaultValueLiteral(expression))
            {
                return;
            }

            mutations.Add(Create(
                text,
                expression.SpanStart,
                expression.Span.Length,
                "return-default",
                Shorten(expression.ToString()),
                "default"));
        }

        private static void AddStatementRemoval(ExpressionStatementSyntax statement, SourceText text, List<Mutation> mutations)
        {
            var expression = statement.Expression;
            if (expression is AwaitExpressionSyntax await)
            {
                expression = await.Expression;
            }

            if (!(expression is InvocationExpressionSyntax invocation))
            {
                return;
            }

            // Лог — побочный канал диагностики, тестами он не наблюдается (см. Logger.md).
            // Удаление Log-вызова эквивалентно по построению: такой «выживший» — гарантированный
            // шум, который вытеснит из отчёта настоящие дыры в ассертах.
            if (IsLogCall(invocation))
            {
                return;
            }

            // Инструкция без фигурных скобок (if (x) Foo();) — удаление оставит if без тела.
            if (!(statement.Parent is BlockSyntax))
            {
                return;
            }

            mutations.Add(Create(
                text,
                statement.SpanStart,
                statement.Span.Length,
                "statement-removal",
                Shorten(statement.ToString()),
                string.Empty));
        }

        // --- Скоуп ---------------------------------------------------------------------------

        // Мутации ограничены членами, которых коснулся диф: полный прогон по сборке не влезает
        // ни в какой разумный таймаут. Пустой список строк означает «весь файл».
        private static List<TextSpan> BuildScope(SyntaxNode root, SourceText text, IReadOnlyCollection<int> changedLines)
        {
            if (changedLines == null || changedLines.Count == 0)
            {
                return null;
            }

            var spans = new List<TextSpan>();

            foreach (var line in changedLines)
            {
                if (line < 1 || line > text.Lines.Count)
                {
                    continue;
                }

                var lineSpan = text.Lines[line - 1].Span;
                var token = root.FindToken(lineSpan.Start);
                var member = token.Parent?
                    .AncestorsAndSelf()
                    .FirstOrDefault(node => node is BaseMethodDeclarationSyntax
                                            || node is PropertyDeclarationSyntax
                                            || node is IndexerDeclarationSyntax
                                            || node is FieldDeclarationSyntax);

                // Правка вне члена (объявление типа, using, атрибут) расширять некуда:
                // берём саму строку, а не класс целиком.
                spans.Add(member?.Span ?? lineSpan);
            }

            return spans;
        }

        private static bool InScope(List<TextSpan> scope, TextSpan span)
        {
            return scope == null || scope.Any(allowed => allowed.IntersectsWith(span));
        }

        // --- Вспомогательное -----------------------------------------------------------------

        private static Mutation Create(
            SourceText text,
            int spanStart,
            int spanLength,
            string @operator,
            string original,
            string mutated)
        {
            var position = text.Lines.GetLinePosition(spanStart);

            return new Mutation
            {
                Line = position.Line + 1,
                Column = position.Character + 1,
                Operator = @operator,
                Original = original,
                Mutated = mutated,
                SpanStart = spanStart,
                SpanLength = spanLength,
                Preview = Shorten(text.Lines[position.Line].ToString().Trim()),
            };
        }

        private static bool IsDefaultValueLiteral(ExpressionSyntax expression)
        {
            if (!(expression is LiteralExpressionSyntax literal))
            {
                return false;
            }

            if (literal.IsKind(SyntaxKind.FalseLiteralExpression) || literal.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return true;
            }

            if (!literal.IsKind(SyntaxKind.NumericLiteralExpression))
            {
                return false;
            }

            // Ноль в любой записи (0, 0f, 0.0m, 0x0) равен default своего числового типа.
            var value = literal.Token.Value;
            return value is int number
                ? number == 0
                : value != null && double.TryParse(
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed) && parsed == 0;
        }

        private static bool IsLogCall(InvocationExpressionSyntax invocation)
        {
            var name = invocation.Expression is MemberAccessExpressionSyntax member
                ? member.Name.Identifier.Text
                : (invocation.Expression as IdentifierNameSyntax)?.Identifier.Text;

            return name != null && name.StartsWith("Log", StringComparison.Ordinal);
        }

        private static bool ContainsTextLiteral(SyntaxNode node)
        {
            return node.DescendantNodesAndSelf().Any(child =>
                child is InterpolatedStringExpressionSyntax
                || (child is LiteralExpressionSyntax literal
                    && (literal.IsKind(SyntaxKind.StringLiteralExpression)
                        || literal.IsKind(SyntaxKind.CharacterLiteralExpression))));
        }

        private static string Shorten(string value)
        {
            var single = string.Join(" ", value.Split('\n').Select(line => line.Trim())).Trim();
            return single.Length <= 90 ? single : single.Substring(0, 87) + "...";
        }
    }
}
