using System.Diagnostics;
using System.Globalization;
using System.Text;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Rendering;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Export;

/// <summary>Выгрузка видео. Вызывается из модели представления, реализация - <see cref="Mp4Exporter"/>.</summary>
public interface IVideoExporter
{
    Task<VideoExportResult> ExportAsync(
        VideoExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// Выгружает видео через внешний ffmpeg: кадры уходят в его stdin непрерывным потоком,
/// аудиодорожка - временным WAV.
/// </summary>
/// <remarks>
/// Рендер идёт на выделенном STA-треде отдельным от вызывающего: <see cref="FrameRenderer"/>
/// требует STA, а вызывающий живёт в потоке интерфейса, где нельзя блокировать.
/// </remarks>
public sealed class Mp4Exporter : IVideoExporter
{
    /// <summary>
    /// Сколько можно не видеть кадров до застоя. На 1080p кадр идёт секунды, и запас нужен
    /// на несколько таких кадров: иначе медленная машина выглядела бы зависшей.
    /// </summary>
    public static readonly TimeSpan DefaultStallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Сколько ждём завершения ffmpeg после последнего кадра. Отдельная от застоя величина:
    /// последняя сборка mp4 идёт уже без нас и занимает время законно.
    /// </summary>
    public static readonly TimeSpan FinishTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Как часто наблюдатель смотрит на счётчик кадров.</summary>
    private const int WatchdogPollMilliseconds = 2000;

    private readonly string? _pathVariable;
    private readonly TimeSpan _stallTimeout;

    /// <param name="pathVariable">
    /// Содержимое переменной PATH для поиска ffmpeg. Не задаётся в приложении, а в тестах
    /// позволяет проверить случай «ffmpeg не найден» независимо от машины.
    /// </param>
    /// <param name="stallTimeout">
    /// Запас времени на кадры. В приложении не задаётся, в тестах подставляется короткий:
    /// иначе проверка застоя ждала бы настоящие полминуты.
    /// </param>
    public Mp4Exporter(string? pathVariable = null, TimeSpan? stallTimeout = null)
    {
        _pathVariable = pathVariable;
        _stallTimeout = stallTimeout ?? DefaultStallTimeout;
    }

    public async Task<VideoExportResult> ExportAsync(
        VideoExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath, nameof(request.OutputPath));

        var settings = SettingsNormalizer.Normalize(request.Settings);
        var export = settings.Export;

        // FfmpegLocator читает PATH из окружения только в конструкторе с одним аргументе,
        // поэтому переменная подставляется здесь. Явный null в конструкторе означал бы «PATH
        // нет», и приложение с пустым полем ffmpeg сообщало бы «ffmpeg не найден», даже
        // когда он есть в PATH.
        var pathVariable = _pathVariable ?? Environment.GetEnvironmentVariable("PATH");
        var ffmpegPath = new FfmpegLocator(export.FfmpegPath, pathVariable).Locate()
            ?? throw new VideoExportException(
                "ffmpeg не найден. Укажите путь к ffmpeg.exe в настройках экспорта или добавьте его в PATH.");

        var outputPath = Path.GetFullPath(request.OutputPath);
        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Аудио ffmpeg не берёт из исходного файла: тот может быть во входном формате,
        // который mp4 не переваривает. Имя у выгрузки своё, а не от результата: два
        // запуска одного файла, и второй обрезал бы WAV, который первый ещё читает.
        var temporaryAudio = Path.Combine(Path.GetTempPath(), $"AudioVisualizer-{Guid.NewGuid():N}.wav");

        var arguments = FfmpegArguments.Build(export, temporaryAudio, outputPath);

        // Команная строка уезжает в текст ошибки. Раньше она лежала в файле рядом с роликом,
        // но файл после выгрузки оставался в папке, а разбор зависания без него шёл слепым.
        var command = FormatCommand(ffmpegPath, arguments);

        var totalFrames = SpectrumFrameSource.FrameCount(request.Audio.Duration, export.FramesPerSecond);
        var layout = new BarLayout(export.Width, export.Height, settings.Bars);

        var frameSource = new SpectrumFrameSource(request.Audio, settings.Spectrum, layout.BarCount);
        var stopwatch = Stopwatch.StartNew();

        Process? process = null;
        StringBuilder errors = new();

        try
        {
            // WAV пишется до старта ffmpeg: запусти мы процесс раньше, он открыл бы файл,
            // которого ещё нет, а File.Create держит его без разделения. Побочная выгода -
            // запись уходит с потока интерфейса, а на треке в четыре минуты идёт секунды.
            await Task.Run(
                () => WavCodec.WritePcm16(temporaryAudio, request.Audio.Samples, request.Audio.SampleRate),
                CancellationToken.None);

            // Отдельное имя нужно, чтобы тип был Process, а не Process?: в лямбде отмены
            // компилятор теряет вывод о присваивании и ругается на возможный null.
            var ffmpeg = StartProcess(ffmpegPath, arguments, out errors);
            process = ffmpeg;

            var stall = new StallWatch(_stallTimeout);

            // Отмена убивает ffmpeg сразу, а не после выхода из цикла кадров. Рендер-поток почти
            // всегда висит внутри записи кадра, пока ffmpeg читает поток, и ждать конца кадра на
            // 1080p - это секунды, в течение которых «отмена» выглядит как «не работает».
            using var killOnCancel = cancellationToken.Register(() => TryKill(ffmpeg));
            using var watchCancellation = new CancellationTokenSource();
            var watchdog = WatchForStallAsync(ffmpeg, stall, watchCancellation.Token);

            try
            {
                var written = await Task.Run(
                    () => WriteFrames(
                        ffmpeg,
                        ffmpeg.StandardInput.BaseStream,
                        frameSource,
                        settings,
                        totalFrames,
                        export.FramesPerSecond,
                        stopwatch,
                        progress,
                        stall,
                        cancellationToken),
                    CancellationToken.None);

                switch (written.Outcome)
                {
                    case FrameWriteOutcome.Cancelled:
                        return new VideoExportResult(outputPath, written.Frames, stopwatch.Elapsed, Cancelled: true);

                    case FrameWriteOutcome.Stalled:
                        throw new VideoExportException(DescribeStall(export.AacCoder, errors, command));

                    case FrameWriteOutcome.ProcessGone:
                        throw new VideoExportException(DescribeFailure(errors, TryGetExitCode(ffmpeg), command));
                }

                // Кадры кончились - закрываем stdin, иначе ffmpeg будет ждать продолжения потока.
                ffmpeg.StandardInput.Close();

                // Токен отмены сюда не идёт намеренно: он означал бы исключение там, где отмена -
                // штатный исход. Своего таймаута достаточно: убитый или зависший процесс ждать нельзя.
                using var finish = new CancellationTokenSource(FinishTimeout);

                try
                {
                    await ffmpeg.WaitForExitAsync(finish.Token);
                }
                catch (OperationCanceledException)
                {
                    TryKill(ffmpeg);

                    throw new VideoExportException(
                        AppendDetails(
                            $"ffmpeg не завершился за {FinishTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} с после последнего кадра.",
                            errors,
                            command));
                }

                if (ffmpeg.ExitCode != 0)
                {
                    throw new VideoExportException(DescribeFailure(errors, ffmpeg.ExitCode, command));
                }

                return new VideoExportResult(outputPath, written.Frames, stopwatch.Elapsed);
            }
            finally
            {
                await watchCancellation.CancelAsync();
                await watchdog;

                // Убитый процесс ещё какое-то время держит файл результата: без этого
                // возврат из выгрузки оставлял mp4 занятым, и его нельзя было ни удалить,
                // ни записать заново.
                await KillAndWaitAsync(ffmpeg);

                ffmpeg.Dispose();
            }
        }
        catch
        {
            // ffmpeg без входного потока висит до вечера: без убийства выгрузка ждала бы
            // завершения процесса вместо немедленного возврата.
            TryKill(process);

            throw;
        }
        finally
        {
            TryDelete(temporaryAudio);
        }
    }

    /// <summary>Команда ffmpeg одной строкой: её можно скопировать и повторить руками.</summary>
    private static string FormatCommand(string ffmpegPath, IEnumerable<string> arguments)
        => string.Join(' ', arguments.Prepend(ffmpegPath));

    private static Process StartProcess(string fileName, IEnumerable<string> arguments, out StringBuilder errors)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var captured = new StringBuilder();
        errors = captured;

        var process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            lock (captured)
            {
                captured.AppendLine(args.Data);
            }
        };

        try
        {
            process.Start();

            // stderr читается асинхронно и постоянно: синхронное чтение заполнит пайп,
            // и ffmpeg встанет посреди выгрузки.
            process.BeginErrorReadLine();
        }
        catch (Exception exception)
        {
            throw new VideoExportException($"Не удалось запустить {fileName}.", exception);
        }

        return process;
    }

    /// <summary>
    /// Следит за кадрами и убивает ffmpeg, если они перестали идти. Наблюдатель отдельный
    /// потоком именно потому, что цикл кадров в этот момент заблокирован записью в пайп:
    /// сам он заметить застой не может.
    /// </summary>
    private static async Task WatchForStallAsync(Process process, StallWatch stall, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(WatchdogPollMilliseconds, token).ConfigureAwait(false);

                if (stall.HasStalled)
                {
                    TryKill(process);

                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Наблюдатель остановлен вместе с выгрузкой: это его штатное завершение.
        }
    }

    private static FrameWriteResult WriteFrames(
        Process process,
        Stream stream,
        SpectrumFrameSource frameSource,
        AppSettings settings,
        int totalFrames,
        int framesPerSecond,
        Stopwatch stopwatch,
        IProgress<VideoExportProgress>? progress,
        StallWatch stall,
        CancellationToken cancellationToken)
    {
        return RunOnStaThread(() =>
        {
            var export = settings.Export;
            // Частота кадров тоже сюда: след затухает по времени, и без неё его длина в ролике
            // зависела бы от пресета выгрузки.
            var renderer = new FrameRenderer(
                export.Width,
                export.Height,
                settings.Colors,
                settings.Bars,
                export.FramesPerSecond);

            for (var frame = 0; frame < totalFrames; frame++)
            {
                // Отмена проверяется без броска исключения: здесь она ожидаемый исход,
                // а бросок на STA-потоке выглядел бы в отладчике как падение приложения.
                if (cancellationToken.IsCancellationRequested)
                {
                    return new FrameWriteResult(frame, FrameWriteOutcome.Cancelled);
                }

                // ffmpeg мог умереть, а следующая запись в пайп ещё прошла бы из буфера.
                // Без проверки мы дорисовали бы целый кадр в никуда.
                if (!IsRunning(process))
                {
                    return new FrameWriteResult(frame, FrameWriteOutcome.ProcessGone);
                }

                var position = SpectrumFrameSource.FrameTime(frame, framesPerSecond);
                var frameBytes = renderer.Render(frameSource.GetBars(position));

                try
                {
                    stream.Write(frameBytes.Span);
                }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException)
                {
                    // Запись оборвалась. Причины три, и по типу исключения они неразличимы:
                    // пользователь нажал отмену, ffmpeg убит наблюдателем из-за застоя или он
                    // умер сам. Различаем по источнику.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new FrameWriteResult(frame, FrameWriteOutcome.Cancelled);
                    }

                    return new FrameWriteResult(
                        frame,
                        stall.HasStalled ? FrameWriteOutcome.Stalled : FrameWriteOutcome.ProcessGone);
                }

                stall.ReportProgress();

                // После отмены прогресс не растёт: полоса не должна продолжать ползти
                // после нажатия «отмена».
                if (!cancellationToken.IsCancellationRequested)
                {
                    progress?.Report(CreateProgress(frame + 1, totalFrames, stopwatch.Elapsed));
                }
            }

            return new FrameWriteResult(totalFrames, FrameWriteOutcome.Completed);
        });
    }

    /// <summary>
    /// Итог прохода по кадрам. Отмена и застой приходят сюда значением, а не исключением:
    /// оба ожидаемы, и источник наверху различает их по этому признаку.
    /// </summary>
    private readonly record struct FrameWriteResult(int Frames, FrameWriteOutcome Outcome);

    /// <summary>Чем кончился проход по кадрам.</summary>
    private enum FrameWriteOutcome
    {
        /// <summary>Кадры записаны все.</summary>
        Completed,

        /// <summary>Пользователь нажал отмену.</summary>
        Cancelled,

        /// <summary>ffmpeg жив, но кадры перестали идти.</summary>
        Stalled,

        /// <summary>ffmpeg умер, не дочитав кадры.</summary>
        ProcessGone
    }

    private static VideoExportProgress CreateProgress(int framesDone, int totalFrames, TimeSpan elapsed)
    {
        TimeSpan? remaining = null;

        if (framesDone > 0 && elapsed > TimeSpan.Zero)
        {
            // Оценка по средней скорости: рендер кадра ровный, а скорость ffmpeg зависит
            // от настроек кодека, но точнее здесь всё равно не получится.
            var perFrame = elapsed / framesDone;
            remaining = perFrame * (totalFrames - framesDone);
        }

        return new VideoExportProgress(framesDone, totalFrames, elapsed, remaining);
    }

    private static T RunOnStaThread<T>(Func<T> function)
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
            IsBackground = true,
            Name = "Video export render"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.GetAwaiter().GetResult();
    }

    private static bool IsRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Что сказать про застой. Кодировщик называется тот, на котором он случился: зависнуть
    /// может любой из трёх, и мы это проверяли. Альтернативы перечисляются без указания,
    /// какой из них лучше: проверено лишь, что два других не зависали на том же треке.
    /// </summary>
    internal static string DescribeStall(string aacCoder, StringBuilder errors, string command)
    {
        var alternatives = ExportSettings.AacCoders
            .Where(coder => !string.Equals(coder, aacCoder, StringComparison.OrdinalIgnoreCase));

        var message = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"ffmpeg перестал читать кадры: кодировщик звука aac ({aacCoder}) завис на этом треке. ")
            .Append(CultureInfo.InvariantCulture, $"Смена кодировщика в настройках выгрузки может помочь ({string.Join(" или ", alternatives)}).")
            .ToString();

        return AppendDetails(message, errors, command);
    }

    private static string DescribeFailure(StringBuilder errors, int? exitCode, string command)
    {
        var prefix = exitCode is null
            ? "ffmpeg остановился, не дочитав кадры."
            : $"ffmpeg завершился с кодом {exitCode.Value.ToString(CultureInfo.InvariantCulture)}:";

        return AppendDetails(prefix, errors, command);
    }

    /// <summary>
    /// Дописывает к ошибке слова ffmpeg и саму команду. Раньше то и другое лежало в файле рядом
    /// с роликом, но файл после выгрузки оставался в папке, и разбор шёл слепым: не видно ни
    /// что сказал ffmpeg, ни чем он запускался. В окне и то и другое копируется от руки.
    /// </summary>
    private static string AppendDetails(string message, StringBuilder errors, string command)
    {
        var builder = new StringBuilder(message);

        var tail = ErrorTail(errors);

        if (tail is not null)
        {
            builder.Append(Environment.NewLine).Append(tail);
        }

        return builder
            .Append(Environment.NewLine).Append(Environment.NewLine)
            .Append(CultureInfo.InvariantCulture, $"Команда ffmpeg:{Environment.NewLine}{command}")
            .ToString();
    }

    private static string? ErrorTail(StringBuilder errors)
    {
        string message;

        lock (errors)
        {
            message = errors.ToString().Trim();
        }

        return message.Length == 0
            ? null
            : message.Length > 800 ? message[^800..] : message;
    }

    private static void TryKill(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Процесс уже завершился или недоступен: это не мешает выбросить исходную ошибку.
        }
    }

    /// <summary>
    /// Убивает процесс и ждёт, пока он освободит свои файлы. Kill возвращается сразу, а
    /// дескрипторы умирают процесса ещё держатся: без ожидания выгрузка возвращалась с
    /// занятым mp4 на диске.
    /// </summary>
    private static async Task KillAndWaitAsync(Process process)
    {
        TryKill(process);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Процесс не умер за пять секунд: возвращаем управление всё равно, ждать дальше
            // пользователь не захочет.
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Процесс недоступен, а значит и файлы он не держит.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            // Временный файл останется в папке temp: это незаметно и не повод ронять выгрузку.
        }
    }

    private static bool IsFileProblem(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}