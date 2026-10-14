namespace AudioVisualizer.Tests;

/// <summary>
/// Запуск действия в потоке с STA. Элементы управления WPF требуют STA, а xUnit выполняет
/// тесты в пуле потоков MTA. Отдельный поток на тест дешевле, чем поднимать STA-диспетчер,
/// и исключения из действия всплывают в тесте как обычно.
/// </summary>
internal static class Sta
{
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Execute(() =>
        {
            action();

            return true;
        });
    }

    public static T Get<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return Execute(function);
    }

    private static T Execute<T>(Func<T> function)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(function());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.GetAwaiter().GetResult();
    }
}