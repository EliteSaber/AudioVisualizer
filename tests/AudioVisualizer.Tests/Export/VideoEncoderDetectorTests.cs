using System.Text.RegularExpressions;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Export;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Поиск кодировщиков видеокарты. Команда пробы проверяется целиком и без ffmpeg: опечатка в
/// ней выглядела бы как «видеокарта не найдена» на любой машине.
/// </summary>
public partial class VideoEncoderDetectorTests
{
    [Fact]
    public void Проба_КодируетОдинКадрИНичегоНеПишет()
    {
        var arguments = VideoEncoderDetector.ProbeArguments("hevc_nvenc").ToArray();

        AssertPair(arguments, "-c:v", "hevc_nvenc");
        AssertPair(arguments, "-frames:v", "1");

        // Источник рисует кадр сам: без lavfi пробе понадобился бы файл.
        Assert.Contains("lavfi", arguments);

        // -f null отправляет кадр в пустоту: проба не должна оставлять файлов нигде.
        Assert.Equal("-", arguments[^1]);
        Assert.Equal("null", arguments[^2]);
    }

    [Fact]
    public void Проба_НеЧитаетСтандартныйВвод()
    {
        // Без -nostdin ffmpeg читает команды со стандартного ввода и забирает байты у вызывающего.
        Assert.Contains("-nostdin", VideoEncoderDetector.ProbeArguments("h264_amf"));
    }

    [Fact]
    public void ПустоеИмяКодировщика_Отклоняется()
    {
        Assert.Throws<ArgumentException>(() => VideoEncoderDetector.ProbeArguments(" "));
    }

    [Theory]
    [InlineData("libx264", "h264")]
    [InlineData("libx265", "hevc")]
    [InlineData(" LIBX265 ", "hevc")]
    [InlineData("h264_nvenc", "h264")]
    [InlineData("hevc_qsv", "hevc")]
    [InlineData("hevc_amf", "hevc")]
    public void СемействоКодека_ОдинаковоУНастроекИКодировщика(string name, string expected)
    {
        Assert.Equal(expected, VideoEncoderDetector.Family(name));
    }

    [Theory]
    [InlineData("libvpx-vp9")]
    [InlineData("h264_mf")]
    [InlineData("h264_something")]
    [InlineData("")]
    [InlineData(null)]
    public void ЧужоеИмя_СемействаНеИмеет(string? name)
    {
        // h264_mf и прочие кодировщики Windows в список не входят: это не то, что мы ищем.
        Assert.Null(VideoEncoderDetector.Family(name));
    }

    [Fact]
    public async Task ПутьКFfmpegНеНайден_СписокПустойИБезОшибки()
    {
        // Отсутствие ffmpeg - не ошибка настроек: панель просто останется с двумя пунктами,
        // а о самом ffmpeg выгрузка скажет отдельно и внятнее. Различать надо не молчание,
        // а причину: без ffmpeg видеокарта ни при чём, и говорить «видеокарты нет» нельзя.
        var detector = new VideoEncoderDetector();

        var detection = await detector.DetectAsync(@"Z:\нет\такого\ffmpeg.exe", "libx264", CancellationToken.None);

        Assert.Empty(detection.Supported);
        Assert.False(detection.FfmpegAvailable);
    }

    [Fact]
    public async Task НеизвестныйКодек_СписокПустой()
    {
        var detector = new VideoEncoderDetector();

        var detection = await detector.DetectAsync(null, "libvpx-vp9", CancellationToken.None);

        Assert.Empty(detection.Supported);
    }

    [Fact]
    public async Task ПустойПуть_ДаётТотЖеОтветЧтоЯвный()
    {
        // Пустая строка от панели и null от выгрузки означают одно и то же — «ищи в PATH».
        // Раньше пустая строка доходила до ProcessStartInfo как есть, процесс падал с
        // InvalidOperationException, и панель объявляла отсутствие видеокарты на машине,
        // где карта была и ffmpeg лежал в PATH.
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var expected = await new VideoEncoderDetector().DetectAsync(ffmpeg, "libx264", CancellationToken.None);

        var empty = await new VideoEncoderDetector().DetectAsync(string.Empty, "libx264", CancellationToken.None);
        var nullPath = await new VideoEncoderDetector().DetectAsync(null, "libx264", CancellationToken.None);

        Assert.Equal(expected.Supported, empty.Supported);
        Assert.Equal(expected.FfmpegAvailable, empty.FfmpegAvailable);
        Assert.Equal(expected.Supported, nullPath.Supported);
    }

    [Fact]
    public async Task ПовторныйСпрос_НеЗапускаетПробуСнова()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var detector = new VideoEncoderDetector();

        var first = await detector.DetectAsync(ffmpeg, "libx264", CancellationToken.None);
        var second = await detector.DetectAsync(ffmpeg, "libx264", CancellationToken.None);

        // Кэш держит один и тот же список: панель и выгрузка спрашивают одно и то же, а каждая
        // проба - это запуски процессов.
        Assert.Same(first, second);
    }

    /// <summary>
    /// Если на машине есть рабочая видеокарта, зонд обязан её назвать, а имя кодировщика
    /// должно быть настоящим. На машине без карты тест выходит молча: кодировщик — внешнее
    /// условие, как и сам ffmpeg.
    /// </summary>
    /// <remarks>
    /// Сначала тест проходил вхолостую, и это была не лень проверять, а ровно то, что он и
    /// ловит: h264_nvenc и hevc_nvenc были в списке кодировщиков, но не кодировали ничего —
    /// драйвер 566.03 старее, чем требовала сборка ffmpeg (610.00 и новее). После обновления
    /// драйвера до 617.42 карта находится, и проверки ниже становятся настоящими.
    /// </remarks>
    [Fact]
    public async Task РабочаяВидеокарта_НаходитсяНастоящейПробой()
    {
        var ffmpeg = new FfmpegLocator().Locate();

        if (ffmpeg is null)
        {
            return;
        }

        var detector = new VideoEncoderDetector();
        var listed = ListedEncoders(ffmpeg);

        foreach (var codec in new[] { "libx264", "libx265" })
        {
            var found = await detector.DetectAsync(ffmpeg, codec, CancellationToken.None);

            // Непустой список сам по себе доказывает, что ffmpeg запустился, и отдельная
            // проверка ради него не делается.
            Assert.True(found.FfmpegAvailable);

            foreach (var support in found.Supported)
            {
                // Найденный кодировщик обязан быть в самом ffmpeg: иначе имя склеено неверно
                // и проба сообщила бы о кодировщике, которого нет.
                Assert.Contains(support.Encoder, listed);

                // Проба обязана совпадать с тем, что получается при прямом запуске: найдено
                // только то, что действительно закодировало кадр.
                Assert.Equal(0, RunProbe(ffmpeg, support.Encoder));
            }
        }
    }

    /// <summary>Кодировщики, о которых знает сборка ffmpeg.</summary>
    private static string[] ListedEncoders(string ffmpegPath)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-encoders");

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);

        // Строка списка устроена так: «флаги, имя кодировщика, описание», а над списком идёт
        // легенда, где на втором месте стоит знак равенства. Отбирать надо по имени, а не по
        // флагам: легенда и список выглядят одинаково, и фильтр по первому слову набирал
        // в ответ кучу знаков «=» вместо названий.
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length > 2 && EncoderName().IsMatch(parts[1]))
            .Select(parts => parts[1])
            .ToArray();
    }

    [GeneratedRegex("[A-Za-z0-9_]+")]
    private static partial Regex EncoderName();

    private static int RunProbe(string ffmpegPath, string encoder)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in VideoEncoderDetector.ProbeArguments(encoder))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);

        return process.ExitCode;
    }

    [Fact]
    public void Вендоры_СодержатТолькоТеЗначенияЧтоВНастройках()
    {
        // Значение вендора уезжает в файл настроек, поэтому список один на всех: разъехавшиеся
        // значения нормализация откатила бы в «программно» молча.
        foreach (var vendor in VideoEncoderDetector.Vendors)
        {
            Assert.Contains(vendor.Vendor, AudioVisualizer.Core.Settings.ExportSettings.VideoEncoders);
            Assert.Contains(vendor.Vendor, HardwareEncoderOptions.Suffixes);
            Assert.False(string.IsNullOrWhiteSpace(vendor.Title));
        }
    }

    private static void AssertPair(string[] arguments, string key, string value)
    {
        var index = Array.IndexOf(arguments, key);

        Assert.True(index > 0, $"в пробе нет {key}");
        Assert.Equal(value, arguments[index + 1]);
    }
}