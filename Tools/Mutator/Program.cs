using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mutator
{
    // Мутатор исходников: строит план мутаций по изменённым строкам и выдаёт мутированный файл
    // по номеру мутанта. Прогоном управляет Tools/mutation-check.ps1 — здесь только текст.
    //
    //   Mutator plan  --out <plan.jsonl> --source <file> [--lines 12-30,44-44] [--source ...]
    //   Mutator apply --source <file> [--lines ...] --index <n> --out <mutated.cs>
    //   Mutator scan  --out <excluded.txt> --rsp <assembly.rsp> [--rsp ...]
    //
    // apply не читает план: планировщик детерминирован, поэтому мутант восстанавливается
    // пересчётом. Так план и применение не могут разъехаться из-за устаревшего файла.
    //
    // scan отделён от plan сознательно: он решает судьбу файла целиком, до планирования, и потому
    // не участвует в нумерации мутантов — plan и apply остаются чисто синтаксическими и
    // детерминированными, как того требует восстановление мутанта по индексу.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Использование: Mutator plan|apply ...");
                return 2;
            }

            try
            {
                switch (args[0])
                {
                    case "plan":
                        return RunPlan(args);

                    case "apply":
                        return RunApply(args);

                    case "scan":
                        return RunScan(args);

                    default:
                        Console.Error.WriteLine($"Неизвестная команда: {args[0]}");
                        return 2;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
                return 2;
            }
        }

        private static int RunPlan(string[] args)
        {
            var targets = ParseTargets(args, out var outPath);
            if (outPath == null)
            {
                Console.Error.WriteLine("plan требует --out <plan.jsonl>.");
                return 2;
            }

            var lines = new List<string>();

            foreach (var target in targets)
            {
                var source = File.ReadAllText(target.Path);

                foreach (var mutation in MutationPlanner.Plan(source, target.Lines))
                {
                    lines.Add(Serialize(target.Path, mutation));
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllLines(outPath, lines, new UTF8Encoding(false));
            Console.WriteLine($"мутаций: {lines.Count}");
            return 0;
        }

        private static int RunApply(string[] args)
        {
            var targets = ParseTargets(args, out var outPath);
            var index = -1;

            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--index")
                {
                    index = int.Parse(args[++i]);
                }
            }

            if (targets.Count != 1 || outPath == null || index < 0)
            {
                Console.Error.WriteLine("apply требует ровно один --source, --index <n> и --out <file>.");
                return 2;
            }

            var target = targets[0];
            var source = File.ReadAllText(target.Path);
            var mutations = MutationPlanner.Plan(source, target.Lines);

            if (index >= mutations.Count)
            {
                Console.Error.WriteLine($"Мутант {index} отсутствует: в плане {mutations.Count}.");
                return 2;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllText(outPath, MutationPlanner.Apply(source, mutations[index]), new UTF8Encoding(false));
            return 0;
        }

        private static int RunScan(string[] args)
        {
            string outPath = null;
            var responseFiles = new List<string>();

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--rsp":
                        responseFiles.Add(Path.GetFullPath(args[++i]));
                        break;

                    case "--out":
                        outPath = args[++i];
                        break;
                }
            }

            if (outPath == null || responseFiles.Count == 0)
            {
                Console.Error.WriteLine("scan требует --out <file> и хотя бы один --rsp <assembly.rsp>.");
                return 2;
            }

            var lines = new List<string>();

            foreach (var responseFile in responseFiles)
            {
                foreach (var excluded in TypeScanner.Scan(responseFile))
                {
                    lines.Add(string.Join("\t", excluded.Reason, excluded.Type, excluded.Path));
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllLines(outPath, lines, new UTF8Encoding(false));
            Console.WriteLine($"файлов вне мутации: {lines.Count}");
            return 0;
        }

        // --- Разбор аргументов ---------------------------------------------------------------

        private sealed class Target
        {
            public string Path;
            public List<int> Lines = new List<int>();
        }

        private static List<Target> ParseTargets(string[] args, out string outPath)
        {
            var targets = new List<Target>();
            outPath = null;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--source":
                        targets.Add(new Target { Path = System.IO.Path.GetFullPath(args[++i]) });
                        break;

                    case "--lines":
                        if (targets.Count == 0)
                        {
                            throw new ArgumentException("--lines указан до первого --source.");
                        }

                        targets[targets.Count - 1].Lines.AddRange(ParseLines(args[++i]));
                        break;

                    case "--out":
                        outPath = args[++i];
                        break;
                }
            }

            return targets;
        }

        // Формат диапазонов — «12-30,44-44,51»: ровно то, что даёт git diff -U0.
        private static IEnumerable<int> ParseLines(string value)
        {
            foreach (var part in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bounds = part.Split('-');
                var start = int.Parse(bounds[0]);
                var end = bounds.Length > 1 ? int.Parse(bounds[1]) : start;

                for (var line = start; line <= end; line++)
                {
                    yield return line;
                }
            }
        }

        // --- Вывод -----------------------------------------------------------------------------

        private static string Serialize(string path, Mutation mutation)
        {
            return string.Concat(
                "{\"file\":\"", Escape(path),
                "\",\"index\":", mutation.Index,
                ",\"line\":", mutation.Line,
                ",\"column\":", mutation.Column,
                ",\"operator\":\"", Escape(mutation.Operator),
                "\",\"original\":\"", Escape(mutation.Original),
                "\",\"mutated\":\"", Escape(mutation.Mutated),
                "\",\"preview\":\"", Escape(mutation.Preview), "\"}");
        }

        private static string Escape(string value)
        {
            var builder = new StringBuilder(value.Length + 8);

            foreach (var symbol in value)
            {
                switch (symbol)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (symbol < ' ')
                        {
                            builder.Append("\\u").Append(((int)symbol).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(symbol);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
