using System.Diagnostics;

namespace AudioVisualizer.Core.Export;

/// <summary>
/// Следит, идут ли кадры: каждая удачная запись сбрасывает отсчёт, и застой наступает,
/// когда очередной счёт не обновился за отведённое время.
/// </summary>
/// <remarks>
/// Отдельный класс нужен из-за устройства цикла кадров: он застревает внутри записи в пайп,
/// пока ffmpeg не читает поток, и до следующей итерации не доходит. Проверять застой прямо
/// в цикле бесполезно - проверить некогда. Поэтому состояние держится здесь, а смотрит на него
/// кто-то снаружи.
/// </remarks>
public sealed class StallWatch
{
    private static readonly Stopwatch SharedClock = Stopwatch.StartNew();

    private readonly TimeSpan _timeout;
    private readonly Func<TimeSpan> _clock;
    private readonly Lock _gate = new();

    private TimeSpan _lastProgress;
    private bool _stalled;

    /// <param name="timeout">Сколько можно не видеть кадров до застоя.</param>
    /// <param name="clock">
    /// Откуда берётся время. По умолчанию - общие часы процесса; тестам передаётся свой,
    /// иначе пришлось бы ждать настоящие полминуты.
    /// </param>
    public StallWatch(TimeSpan timeout, Func<TimeSpan>? clock = null)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Время ожидания кадра должно быть положительным.");
        }

        _timeout = timeout;
        _clock = clock ?? (Func<TimeSpan>)(static () => SharedClock.Elapsed);

        // Отсчёт с момента создания: до первого кадра тоже нужен запас, иначе выгрузка,
        // которая не успела начать, сочтётся зависшей.
        _lastProgress = _clock();
    }

    /// <summary>Застой уже зафиксирован. Сбрасывания нет: застой не разжимается сам.</summary>
    public bool HasStalled
    {
        get
        {
            lock (_gate)
            {
                if (!_stalled)
                {
                    _stalled = _clock() - _lastProgress >= _timeout;
                }

                return _stalled;
            }
        }
    }

    /// <summary>Кадр записан: отсчёт начинается заново.</summary>
    public void ReportProgress()
    {
        lock (_gate)
        {
            if (!_stalled)
            {
                _lastProgress = _clock();
            }
        }
    }
}