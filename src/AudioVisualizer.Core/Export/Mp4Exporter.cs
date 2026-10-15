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
/// аудиодорожка - временным WAV рядом с результатом.
/// </summary>
/// <remarks>
/// Рендер идёт на выделенном STA-треде отдельным от вызывающего: <see cref="FrameRenderer"/>
/// требует STA, а вызывающий живёт в потоке интерфейса, где нельзя блокировать.
/// </remarks>
public sealed class Mp4Exporter : IVideoExporter
{
    private readonly string? _pathVariable;

    /// <param name="pathVariable">
    /// Содержимое переменной PATH для поиска ffmpeg. Не задаётся в приложении, а в тестах
    /// позволяет проверить случай «ffmpeg не найден» независимо от машины.
    /// </param>
    public Mp4Exporter(string? pathVariable = null) => _pathVariable = pathVariable;

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
        // который mp4 не переваривает. Поэтому рядом с результатом пишется временный WAV.
        var temporaryAudio = Path.Combine(
            directory ?? AppContext.BaseDirectory,
            Path.GetFileNameWithoutExtension(outputPath) + ".wav");

        var arguments = FfmpegArguments.Build(export, temporaryAudio, outputPath);
        var totalFrames = SpectrumFrameSource.FrameCount(request.Audio.Duration, export.FramesPerSecond);
        var layout = new BarLayout(export.Width, export.Height, settings.Bars);

        var frameSource = new SpectrumFrameSource(request.Audio, settings.Spectrum, layout.BarCount);
        var stopwatch = Stopwatch.StartNew();

        using var process = StartProcess(ffmpegPath, arguments, out var errors);

        // Отмена убивает ffmpeg сразу, а не после выхода из цикла кадров. Рендер-поток почти
        // всегда висит внутри записи кадра, пока ffmpeg читает поток, и ждать конца кадра на
        // 1080p - это секунды, в течение которых «отмена» выглядит как «не работает».
        using var killOnCancel = cancellationToken.Register(() => TryKill(process));

        try
        {
            WavCodec.WritePcm16(temporaryAudio, request.Audio.Samples, request.Audio.SampleRate);

            var written = await Task.Run(
                () => WriteFrames(
                    process.StandardInput.BaseStream,
                    frameSource,
                    settings,
                    totalFrames,
                    export.FramesPerSecond,
                    stopwatch,
                    progress,
                    cancellationToken),
                CancellationToken.None);

            if (written.Cancelled)
            {
                return new VideoExportResult(outputPath, written.Frames, stopwatch.Elapsed, Cancelled: true);
            }

            // Кадры кончились - закрываем stdin, иначе ffmpeg будет ждать продолжения потока.
            process.StandardInput.Close();

            // Токен сюда не передаётся намеренно: ffmpeg уже убит или вот-вот завершится сам,
            // а ожидание с токеном бросило бы исключение там, где отмена - штатный исход.
            await process.WaitForExitAsync(CancellationToken.None);

            if (process.ExitCode != 0)
            {
                throw new VideoExportException(DescribeFailure(errors, process.ExitCode));
            }

            return new VideoExportResult(outputPath, written.Frames, stopwatch.Elapsed);
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

    private static FrameWriteResult WriteFrames(
        Stream stream,
        SpectrumFrameSource frameSource,
        AppSettings settings,
        int totalFrames,
        int framesPerSecond,
        Stopwatch stopwatch,
        IProgress<VideoExportProgress>? progress,
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
                    return new FrameWriteResult(frame, Cancelled: true);
                }

                var position = SpectrumFrameSource.FrameTime(frame, framesPerSecond);
                var frameBytes = renderer.Render(frameSource.GetBars(position));

                try
                {
                    stream.Write(frameBytes.Span);
                }
                catch (Exception exception) when (
                    cancellationToken.IsCancellationRequested &&
                    exception is IOException or ObjectDisposedException)
                {
                    // Запись оборвалась потому, что ffmpeg убит по требованию отмены: это
                    // тот же исход, только с другой стороны.
                    return new FrameWriteResult(frame, Cancelled: true);
                }

                // После отмены прогресс не растёт: полоса не должна продолжать ползти
                // после нажатия «отмена».
                if (!cancellationToken.IsCancellationRequested)
                {
                    progress?.Report(CreateProgress(frame + 1, totalFrames, stopwatch.Elapsed));
                }
            }

            return new FrameWriteResult(totalFrames, Cancelled: false);
        });
    }

    /// <summary>
    /// Итог прохода по кадрам. Отмена приходит сюда значением, а не исключением: она
    /// ожидаема, и источник отмены наверху окна различает её по этому признаку.
    /// </summary>
    private readonly record struct FrameWriteResult(int Frames, bool Cancelled);

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

    private static string DescribeFailure(StringBuilder errors, int exitCode)
    {
        string message;

        lock (errors)
        {
            message = errors.ToString().Trim();
        }

        var tail = message.Length > 800 ? message[^800..] : message;

        return string.IsNullOrEmpty(tail)
            ? $"ffmpeg завершился с кодом {exitCode.ToString(CultureInfo.InvariantCulture)}."
            : $"ffmpeg завершился с кодом {exitCode.ToString(CultureInfo.InvariantCulture)}: {tail}";
    }

    private static void TryKill(Process process)
    {
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

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Временный файл останется рядом с видео: это заметно, но не повод ронять выгрузку.
        }
    }
}