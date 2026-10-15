using System.Windows.Threading;

namespace AudioVisualizer.Tests;

/// <summary>
/// Выполняет действия в одном STA-потоке на весь процесс.
/// </summary>
/// <remarks>
/// Поток общий, а не новый на каждый вызов. Элементы WPF, стили и словари привязаны к
/// потоку, который их создал, и проверка доступа падает при обращении к объекту из другого:
/// при своём потоке на вызов кэш стилей жил бы на потоке первой же проверки, а остальные
/// падали бы в зависимости от порядка запуска.
/// </remarks>
internal static class Sta
{
    private static readonly Dispatcher Shared = CreateDispatcher();

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Shared.Invoke(action);
    }

    public static T Get<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return Shared.Invoke(function);
    }

    private static Dispatcher CreateDispatcher()
    {
        var started = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;

        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            started.Set();

            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF tests"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        started.Wait();

        return dispatcher ?? throw new InvalidOperationException("Поток тестов WPF не поднялся.");
    }
}