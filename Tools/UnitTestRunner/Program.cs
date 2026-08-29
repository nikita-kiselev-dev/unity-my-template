using System.Reflection;

// Минимальный NUnit-раннер для EditMode-тестов вне Unity: поддерживает [Test], [SetUp],
// [TearDown], [OneTimeSetUp], [OneTimeTearDown]. Финальная истина — Unity Test Runner.
// Использование: UnitTestRunner --probe <dir> [--probe <dir> ...] [--journal <file>]
//                               <tests1.dll> <tests2.dll> ...

var probeDirs = new List<string>();
var testAssemblies = new List<string>();
var journalPath = (string?)null;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--probe")
    {
        probeDirs.Add(Path.GetFullPath(args[++i]));
    }
    else if (args[i] == "--journal")
    {
        journalPath = Path.GetFullPath(args[++i]);
    }
    else
    {
        testAssemblies.Add(Path.GetFullPath(args[i]));
    }
}

if (testAssemblies.Count == 0)
{
    Console.Error.WriteLine("Не указаны тестовые сборки.");
    return 2;
}

AppDomain.CurrentDomain.AssemblyResolve += (_, resolveArgs) =>
{
    var name = new AssemblyName(resolveArgs.Name).Name + ".dll";

    foreach (var dir in probeDirs)
    {
        var candidate = Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
        if (candidate != null)
        {
            return Assembly.LoadFrom(candidate);
        }
    }

    return null;
};

DisableUnityLogger();

var passed = 0;
var skipped = new List<string>();
var failures = new List<(string test, Exception error)>();

// Журнал прогонов: гейт tdd-check по нему проверяет, что новый тест был красным до зелёного
// и что позеленевший тест не переписали. Тип исключения нужен, чтобы отличить фазу red
// (assert не выполнился) от «кода ещё нет» (NullReference / MissingMethod).
var journal = new List<(string test, string outcome, string errorType)>();

foreach (var assemblyPath in testAssemblies)
{
    var assembly = Assembly.LoadFrom(assemblyPath);

    foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName))
    {
        var tests = GetMethodsWith(type, "TestAttribute");

        if (tests.Length == 0 || type.IsAbstract)
        {
            continue;
        }

        object fixture;

        try
        {
            fixture = Activator.CreateInstance(type);
            InvokeAll(fixture, GetMethodsWith(type, "OneTimeSetUpAttribute"));
        }
        catch (Exception exception)
        {
            var unwrapped = Unwrap(exception);
            failures.Add(($"{type.FullName} (создание фикстуры)", unwrapped));

            // Фикстура не поднялась — красными считаются все её тесты, иначе гейт red-green
            // увидел бы у нового теста пустую историю вместо падения.
            foreach (var test in tests)
            {
                journal.Add(($"{type.FullName}.{test.Name}", "failed", unwrapped.GetType().Name));
            }

            continue;
        }

        foreach (var test in tests)
        {
            var name = $"{type.FullName}.{test.Name}";

            try
            {
                InvokeAll(fixture, GetMethodsWith(type, "SetUpAttribute"));
                test.Invoke(fixture, null);
                passed++;
                journal.Add((name, "passed", string.Empty));
            }
            catch (Exception exception)
            {
                var unwrapped = Unwrap(exception);

                // LogAssert работает только внутри Unity Test Runner — вне Unity такой тест не проверить.
                // Assert.Ignore — сознательный пропуск самим тестом (нет данных для проверки).
                if ((unwrapped is InvalidOperationException && unwrapped.Message.Contains("No log scope"))
                    || unwrapped.GetType().Name == "IgnoreException")
                {
                    skipped.Add(name);
                    journal.Add((name, "skipped", unwrapped.GetType().Name));
                }
                else
                {
                    failures.Add((name, unwrapped));
                    journal.Add((name, "failed", unwrapped.GetType().Name));
                }
            }
            finally
            {
                TryInvokeAll(fixture, GetMethodsWith(type, "TearDownAttribute"));
            }
        }

        TryInvokeAll(fixture, GetMethodsWith(type, "OneTimeTearDownAttribute"));
    }
}

Console.WriteLine($"Пройдено: {passed}, упало: {failures.Count}, пропущено (LogAssert / Assert.Ignore): {skipped.Count}");

foreach (var name in skipped)
{
    Console.WriteLine($"SKIPPED: {name}");
}

foreach (var (test, error) in failures)
{
    Console.WriteLine();
    Console.WriteLine($"FAILED: {test}");
    Console.WriteLine($"  {error.GetType().Name}: {error.Message}");

    var stackLines = (error.StackTrace ?? string.Empty)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Take(4);

    foreach (var line in stackLines)
    {
        Console.WriteLine($"  {line.Trim()}");
    }
}

WriteJournal();

return failures.Count == 0 ? 0 : 1;

void WriteJournal()
{
    if (journalPath == null)
    {
        return;
    }

    var utc = DateTime.UtcNow.ToString("o");
    var lines = journal.Select(entry => string.Concat(
        "{\"utc\":\"", utc,
        "\",\"test\":\"", entry.test,
        "\",\"outcome\":\"", entry.outcome,
        "\",\"errorType\":\"", entry.errorType, "\"}"));

    try
    {
        var directory = Path.GetDirectoryName(journalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllLines(journalPath, lines);

        // Ротация: журнал только дописывается, поэтому без обрезки он растёт бесконечно.
        // Хвоста хватает на историю задолго до текущей задачи.
        const int limit = 20_000;
        var all = File.ReadAllLines(journalPath);
        if (all.Length > limit)
        {
            File.WriteAllLines(journalPath, all.Skip(all.Length - limit));
        }
    }
    catch (Exception exception)
    {
        // Журнал — инструмент гейта, а не результат прогона: его сбой не должен красить тесты.
        Console.WriteLine($"Журнал прогонов не записан: {exception.GetType().Name}: {exception.Message}");
    }
}

static MethodInfo[] GetMethodsWith(Type type, string attributeName)
{
    return type
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(method => method.GetCustomAttributes().Any(a => a.GetType().Name == attributeName))
        .ToArray();
}

static void InvokeAll(object fixture, MethodInfo[] methods)
{
    foreach (var method in methods)
    {
        method.Invoke(fixture, null);
    }
}

static void TryInvokeAll(object fixture, MethodInfo[] methods)
{
    try
    {
        InvokeAll(fixture, methods);
    }
    catch
    {
        // Ошибка в TearDown не должна маскировать результат теста.
    }
}

static Exception Unwrap(Exception exception)
{
    while (exception is TargetInvocationException { InnerException: not null } wrapped)
    {
        exception = wrapped.InnerException;
    }

    return exception;
}

// Debug.Log вне Unity падает на нативном вызове; logEnabled — чисто managed-флаг,
// который отсекает логи до нативного слоя.
static void DisableUnityLogger()
{
    try
    {
        var debugType = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule");
        var logger = debugType?.GetProperty("unityLogger", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        logger?.GetType().GetProperty("logEnabled")?.SetValue(logger, false);
    }
    catch
    {
        // UnityEngine.CoreModule ещё не загружен — отключим при первом резолве не выйдет; просто пропускаем.
    }
}
