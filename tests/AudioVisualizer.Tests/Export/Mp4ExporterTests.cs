using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Analysis;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Сквозная выгрузка короткого ролика настоящим ffmpeg. Это единственный тест, который
/// проверяет связку целиком: рендер, поток кадров, кодирование и наличие аудиодорожки.
/// </summary>
/// <remarks>
/// Тест пропускает себя молча, если ffmpeg не найден: он внешняя зависимость, и падать
/// из-за её отсутствия на машине разработчика незачем. Всё остальное - аргументы, рендер и
/// кадры - проверяется без ffmpeg.
/// </remarks>
public class Mp4ExporterTests
{
    [Theory]
    [InlineData("libx264", "avc1")]
    [InlineData("libx265", "hvc1")]
    public async Task ВыгрузкаСекунды_ДаётMp4СВидеоИЗвуком(string videoCodec, string expectedTag)
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-");
        var output = Path.Combine(directory.FullName, "итог.mp4");

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 15,
                    VideoCodec = videoCodec,
                    Crf = 30,
                    CodecPreset = "ultrafast",
                    AudioBitrateKbps = 64,
                    FfmpegPath = ffmpeg
                },
                Bars = new BarSettings { Width = 8, Spacing = 4, CornerRadius = 0, Margin = 8, CenterGap = 4 }
            };

            var exporter = new Mp4Exporter();

            var result = await exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, output),
                progress: null,
                CancellationToken.None);

            Assert.Equal(15, result.Frames);
            Assert.True(File.Exists(output));

            var bytes = File.ReadAllBytes(output);
            Assert.True(bytes.Length > 1000, "Файл слишком мал: скорее всего, кодировщик ничего не принял.");

            // Контейнер mp4 начинается с бокса ftyp: так проверяется, что файл действительно
            // mp4, а не пустой или оборванный поток.
            Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(bytes, 4, 4));

            // Из-за -movflags +faststart описание потоков стоит в начале файла. Без него можно
            // было бы получить видео без звука или наоборот и не заметить этого по размеру.
            // Тег дорожки приходит из кодека, поэтому он и разный: avc1 у H.264, hvc1 у H.265.
            var head = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 64 * 1024));
            Assert.Contains(expectedTag, head, StringComparison.Ordinal);
            Assert.Contains("mp4a", head, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Выгрузка видеокартой настоящим ffmpeg: команды собирает <see cref="FfmpegArguments"/>,
    /// кодировщика выбирает <see cref="VideoEncoderDetector"/>.
    /// </summary>
    /// <remarks>
    /// Тест пропускает себя молча, если рабочей видеокарты нет: кодировщик — внешнее условие,
    /// и падать из-за его отсутствия незачем. Он проверяет главное — что перевод CRF и пресета
    /// в параметры NVENC не только верен по таблице, но и принимается ffmpeg, а труба кадров
    /// BGRA доезжает до кодировщика видеокарты без изменений.
    /// </remarks>
    [Theory]
    [InlineData("libx264", "avc1")]
    [InlineData("libx265", "hvc1")]
    public async Task ВыгрузкаВидеокартой_ДаётMp4СВидеоИЗвуком(string videoCodec, string expectedTag)
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        // Настоящий зонд: он и есть ответ на вопрос, есть ли здесь рабочая видеокарта.
        var detector = new VideoEncoderDetector();
        var detection = await detector.DetectAsync(ffmpeg, videoCodec, CancellationToken.None);

        if (detection.Supported.All(vendor => vendor.Vendor != "nvenc"))
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-nvenc-");
        var output = Path.Combine(directory.FullName, "итог.mp4");

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 15,
                    VideoCodec = videoCodec,
                    VideoEncoder = "nvenc",
                    Crf = 30,
                    CodecPreset = "ultrafast",
                    AudioBitrateKbps = 64,
                    FfmpegPath = ffmpeg
                },
                Bars = new BarSettings { Width = 8, Spacing = 4, CornerRadius = 0, Margin = 8, CenterGap = 4 }
            };

            var exporter = new Mp4Exporter(encoderDetector: detector);

            var result = await exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, output),
                progress: null,
                CancellationToken.None);

            // Видеокарта не молчит о том, чем кодировала: без этого отход на процессор был бы
            // виден только по времени выгрузки.
            Assert.Equal($"{VideoEncoderDetector.Family(videoCodec)}_nvenc", result.Encoder);
            Assert.False(result.FellBackToCpu);
            Assert.True(File.Exists(output));

            var bytes = File.ReadAllBytes(output);
            Assert.True(bytes.Length > 1000, "Файл слишком мал: скорее всего, кодировщик ничего не принял.");

            // Тег дорожки ставится по кодеку, а не по кодировщику: у видеокарных кодировщиков
            // H.265 по умолчанию тоже уходит в hev1, который не открывают часть плееров.
            var head = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 64 * 1024));
            Assert.Contains(expectedTag, head, StringComparison.Ordinal);
            Assert.Contains("mp4a", head, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ВыгрузкаСПустымПутёмВНастройках_ИщетFfmpegВПеременнойОкружения()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-path-");
        var output = Path.Combine(directory.FullName, "итог.mp4");

        try
        {
            // Сценарий приложения: поле ffmpeg в настройках пустое, файл берётся из PATH.
            // Проверки с явным путём и с «ffmpeg не найден» покрывали противоположные
            // случаи и обе проходили, пока эта связка не сломалась.
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 15,
                    Crf = 30,
                    CodecPreset = "ultrafast",
                    AudioBitrateKbps = 64,
                    FfmpegPath = null
                },
                Bars = new BarSettings { Width = 8, Spacing = 4, CornerRadius = 0, Margin = 8, CenterGap = 4 }
            };

            var exporter = new Mp4Exporter();

            await exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, output),
                progress: null,
                CancellationToken.None);

            Assert.True(File.Exists(output));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Выгрузка_ОтменяетсяПоСигналу()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-cancel-");
        var output = Path.Combine(directory.FullName, "отмена.mp4");

        using var cancellation = new CancellationTokenSource();

        try
        {
            // Ролик длинный, а отмена приходит сразу: успешный случай не успевает
            // записать ни одного кадра, и проверять приходится именно отмену.
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 30,
                    CodecPreset = "ultrafast",
                    FfmpegPath = ffmpeg
                }
            };

            await cancellation.CancelAsync();

            var exporter = new Mp4Exporter();
            var result = await exporter.ExportAsync(
                new VideoExportRequest(Audio(TimeSpan.FromSeconds(30d)), settings, output),
                progress: null,
                cancellation.Token);

            // Отмена - ожидаемый исход, а не ошибка: приходит результатом, без исключения.
            Assert.True(result.Cancelled);
            Assert.Equal(0, result.Frames);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ОтменаНаХоду_ОстанавливаетВыгрузкуДоКонцаРолика()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-mid-");
        var output = Path.Combine(directory.FullName, "отмена.mp4");

        using var cancellation = new CancellationTokenSource();

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 30,
                    CodecPreset = "ultrafast",
                    FfmpegPath = ffmpeg
                }
            };

            var exporter = new Mp4Exporter();
            var export = exporter.ExportAsync(
                new VideoExportRequest(Audio(TimeSpan.FromSeconds(60d)), settings, output),
                progress: null,
                cancellation.Token);

            // Отмена приходит, когда ffmpeg уже читает поток, а рендер-поток висит внутри
            // записи кадра: именно этот случай раньше ждал конца кадра.
            await Task.Delay(300, CancellationToken.None);
            await cancellation.CancelAsync();

            var result = await export.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.True(result.Cancelled);

            // Ролик минуты при 30 кадрах в секунду - это 1800 кадров: выгрузка в любом
            // случае не успела бы дописать его целиком, но и ждать минуту не должна.
            Assert.True(result.Frames < 1800, "Отмена не остановила выгрузку: записаны все кадры.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task БезFfmpeg_ПонятнаяОшибка()
    {
        var directory = Directory.CreateTempSubdirectory("av-export-nof-");

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FfmpegPath = Path.Combine(directory.FullName, "нет-такого.exe")
                }
            };

            // Путь из настроек проверяется первым, а переменная PATH подменена пустой:
            // иначе тест прошёл бы на любой машине, где ffmpeg установлен.
            var exporter = new Mp4Exporter(pathVariable: string.Empty);
            var exception = await Assert.ThrowsAsync<VideoExportException>(() => exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, Path.Combine(directory.FullName, "итог.mp4")),
                progress: null,
                CancellationToken.None));

            Assert.Contains("ffmpeg", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Ответ зонда обязан доехать до сборки команды. Раньше это проверялось по тексту ошибки
    /// от ffmpeg, который падал на сломанном NVENC, и тест был честным только пока карта не
    /// работала: с работающей картой выгрузка проходила, а проверять было нечего.
    /// </summary>
    /// <remarks>
    /// Теперь отказ устраивается на нашей же проверке: зонд возвращает кодировщик не того
    /// кодека, и `FfmpegArguments` отказывает его сам, на любой машине. Если бы ответ зонда
    /// не дошёл до сборки команды, выгрузка молча кодировала бы процессором и тест прошёл бы
    /// вхолостую.
    /// </remarks>
    [Fact]
    public async Task ОтветЗондаДоехалДоСборкиКоманды()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-wire-");
        var output = Path.Combine(directory.FullName, "итог.mp4");

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 15,
                    VideoCodec = "libx264",
                    VideoEncoder = ExportSettings.AutoVideoEncoder,
                    Crf = 30,
                    CodecPreset = "ultrafast",
                    FfmpegPath = ffmpeg
                }
            };

            var exporter = new Mp4Exporter(encoderDetector: new FixedEncoderDetector("hevc_nvenc"));

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, output),
                progress: null,
                CancellationToken.None));

            Assert.Contains("hevc_nvenc", exception.Message, StringComparison.Ordinal);
            Assert.Contains("libx264", exception.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(output));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task НетВидеокарты_ВыгрузкаИдётПроцессоромИГоворитОбЭтом()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-nogpu-");
        var output = Path.Combine(directory.FullName, "итог.mp4");

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 15,
                    VideoEncoder = "nvenc",
                    Crf = 30,
                    CodecPreset = "ultrafast",
                    AudioBitrateKbps = 64,
                    FfmpegPath = ffmpeg
                }
            };

            var exporter = new Mp4Exporter(encoderDetector: new FixedEncoderDetector([]));
            var result = await exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, output),
                progress: null,
                CancellationToken.None);

            // Просили видеокарту, кодировал процессор: ролик получился, но выгрузка обязана
            // сказать об этом - иначе настройка молча не сделала того, о чём просили.
            Assert.Null(result.Encoder);
            Assert.True(result.FellBackToCpu);
            Assert.True(File.Exists(output));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ПрограммноеКодирование_ЗондНеЗовётся()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("av-export-sw-");

        try
        {
            var settings = new AppSettings
            {
                Export = new ExportSettings
                {
                    Width = 320,
                    Height = 240,
                    FramesPerSecond = 15,
                    Crf = 30,
                    CodecPreset = "ultrafast",
                    FfmpegPath = ffmpeg
                }
            };

            var detector = new FixedEncoderDetector([]);
            var exporter = new Mp4Exporter(encoderDetector: detector);

            var result = await exporter.ExportAsync(
                new VideoExportRequest(Audio(), settings, Path.Combine(directory.FullName, "итог.mp4")),
                progress: null,
                CancellationToken.None);

            // При выбранном процессоре искать нечего: каждая проба - это запуски процессов.
            Assert.Equal(0, detector.Calls);
            Assert.Null(result.Encoder);
            Assert.False(result.FellBackToCpu);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Синус 440 Гц нужной длины: спектр ненулевой, и в mp4 попадает и звук, и картинка.</summary>
    private static AudioData Audio(TimeSpan? duration = null)
    {
        const int sampleRate = 44100;

        var fftSize = SpectrumSettings.Default.FftSize;
        var length = (int)((duration ?? TimeSpan.FromSeconds(1d)).TotalSeconds * sampleRate);

        return new AudioData(
            TestSignal.SineOnBin(sampleRate, (int)(440d * fftSize / sampleRate), fftSize, length, 0.8f),
            sampleRate);
    }

    /// <summary>
    /// Зонд с заранее заданным ответом: настоящие пробы запускают ffmpeg, а в тестах выгрузки
    /// важно проверить путь выгрузки, а не наличие карты на машине.
    /// </summary>
    private sealed class FixedEncoderDetector : IVideoEncoderDetector
    {
        private readonly IReadOnlyList<VideoEncoderSupport> _found;

        public FixedEncoderDetector(IReadOnlyList<VideoEncoderSupport> found)
        {
            _found = found;
        }

        public FixedEncoderDetector(string encoder)
            : this([new VideoEncoderSupport("nvenc", "NVIDIA", encoder)])
        {
        }

        public int Calls { get; private set; }

        public Task<VideoEncoderDetection> DetectAsync(
            string? ffmpegPath,
            string videoCodec,
            CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(new VideoEncoderDetection(_found, FfmpegAvailable: true));
        }
    }
}