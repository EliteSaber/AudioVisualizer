using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Командная строка ffmpeg. Проверяется целиком: опечатка в параметре не падает в тестах,
/// а выглядит как «ffmpeg ругается на неизвестный ключ» или как тихое расхождение картинки.
/// </summary>
public class FfmpegArgumentsTests
{
    private static ExportSettings Settings => new()
    {
        Width = 1280,
        Height = 720,
        FramesPerSecond = 30,
        Crf = 20,
        CodecPreset = "fast",
        AudioBitrateKbps = 256
    };

    [Fact]
    public void Build_ВидеоИдётВstdin_АудиоИзФайла()
    {
        var arguments = Build();

        Assert.Contains("pipe:0", arguments);
        Assert.Contains("аудио.wav", arguments);
    }

    [Fact]
    public void Build_ПараметрыВходаСовпадаютСВыходом()
    {
        var arguments = Build();

        // ffmpeg не угадывает размер кадра: без -video_size поток молча читается как 320x240
        // и видео получается со сдвигом или обрезкой.
        Assert.Contains("1280x720", arguments);
        Assert.Contains("30", arguments);
    }

    [Fact]
    public void Build_ФорматКадраСовпадаетСФорматомСнимка()
    {
        var arguments = Build();

        Assert.Contains("bgra", arguments);
        Assert.Contains("yuv420p", arguments);
    }

    [Fact]
    public void Build_КодекИПресетыИзНастроек()
    {
        var arguments = Build();

        Assert.Contains("libx264", arguments);
        Assert.Contains("fast", arguments);
        Assert.Contains("20", arguments);
        Assert.Contains("256k", arguments);
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    public void Build_КодекВидеоСтоитРядомС_Cv(string codec)
    {
        // -c:v берётся из настроек: у обоих кодеков общие -preset и -crf, поэтому
        // переключение кодека не должно менять остальную команду.
        var arguments = FfmpegArguments.Build(Settings with { VideoCodec = codec }, "аудио.wav", "итог.mp4").ToArray();
        var index = Array.IndexOf(arguments, "-c:v");

        Assert.True(index > 0, "в команде нет -c:v");
        Assert.Equal(codec, arguments[index + 1]);
        Assert.Contains("fast", arguments);
        Assert.Contains("20", arguments);
    }

    [Fact]
    public void Build_КодекВидеоПриводитсяКНижнемуРегистру()
    {
        var arguments = FfmpegArguments.Build(Settings with { VideoCodec = " LIBX265 " }, "аудио.wav", "итог.mp4").ToArray();
        var index = Array.IndexOf(arguments, "-c:v");

        Assert.Equal("libx265", arguments[index + 1]);
    }

    [Fact]
    public void H265_ПомечаетсяТегомHvc1()
    {
        // Без тега ffmpeg пишет hev1, а его не открывают QuickTime и часть плееров на
        // телефонах: разница в том, где лежат служебные записи кодека.
        var arguments = FfmpegArguments.Build(Settings with { VideoCodec = "libx265" }, "аудио.wav", "итог.mp4").ToArray();
        var index = Array.IndexOf(arguments, "-tag:v");

        Assert.True(index > 0, "в команде нет -tag:v");
        Assert.Equal("hvc1", arguments[index + 1]);
    }

    [Fact]
    public void H264_ТегДорожкиНеЗадан()
    {
        // avc1 читается везде сам, лишний тег у mp4 с H.264 был бы враньём в команду.
        Assert.DoesNotContain("-tag:v", FfmpegArguments.Build(Settings, "аудио.wav", "итог.mp4"));
    }

    [Theory]
    [InlineData("libx264", "h264_nvenc")]
    [InlineData("libx265", "hevc_nvenc")]
    [InlineData("libx264", "h264_qsv")]
    [InlineData("libx265", "hevc_qsv")]
    [InlineData("libx264", "h264_amf")]
    [InlineData("libx265", "hevc_amf")]
    public void КодекВидеокартыСтоитВместоПрограммного(string codec, string encoder)
    {
        // У кодировщиков видеокарты нет ни -crf, ни нашего списка пресетов: вместо них уходят
        // переведённые значения, иначе ffmpeg ругается на неизвестные ключи.
        var arguments = FfmpegArguments.Build(
            Settings with { VideoCodec = codec, CodecPreset = "fast", Crf = 20 },
            "аудио.wav",
            "итог.mp4",
            encoder);

        AssertPair(arguments, "-c:v", encoder);
        Assert.DoesNotContain("-crf", arguments);
    }

    [Fact]
    public void Nvenc_КачествоПереводитсяВCqИБитрейтОбнуляется()
    {
        // Без -b:v 0 кодировщик режет поток по битрейту, а не по качеству, и ползунок CRF
        // перестаёт что-либо значить.
        var arguments = FfmpegArguments.Build(Settings with { CodecPreset = "slow", Crf = 22 }, "аудио.wav", "итог.mp4", "h264_nvenc").ToArray();

        AssertPair(arguments, "-preset", "p5");
        AssertPair(arguments, "-rc", "vbr");
        AssertPair(arguments, "-cq", "22");
        AssertPair(arguments, "-b:v", "0");
    }

    [Fact]
    public void Qsv_КачествоПереводитсяВGlobalQuality()
    {
        var arguments = FfmpegArguments.Build(Settings with { CodecPreset = "veryfast", Crf = 25 }, "аудио.wav", "итог.mp4", "h264_qsv").ToArray();

        AssertPair(arguments, "-preset", "veryfast");
        AssertPair(arguments, "-global_quality", "25");
        Assert.DoesNotContain("-cq", arguments);
    }

    [Fact]
    public void Amf_КачествоПереводитсяВCqp()
    {
        var arguments = FfmpegArguments.Build(
            Settings with { VideoCodec = "libx265", CodecPreset = "veryslow", Crf = 20 },
            "аудио.wav",
            "итог.mp4",
            "hevc_amf").ToArray();

        AssertPair(arguments, "-quality", "quality");
        AssertPair(arguments, "-rc", "cqp");
        AssertPair(arguments, "-qp_i", "20");
        AssertPair(arguments, "-qp_p", "20");
    }

    [Theory]
    [InlineData("ultrafast", "p1", "veryfast", "speed")]
    [InlineData("superfast", "p1", "veryfast", "speed")]
    [InlineData("veryfast", "p2", "veryfast", "speed")]
    [InlineData("faster", "p3", "faster", "speed")]
    [InlineData("fast", "p4", "fast", "balanced")]
    [InlineData("medium", "p4", "medium", "balanced")]
    [InlineData("slow", "p5", "slow", "quality")]
    [InlineData("slower", "p6", "slower", "quality")]
    [InlineData("veryslow", "p7", "veryslow", "quality")]
    public void ПресетПереводитсяУВсехТрёх(
        string preset,
        string nvenc,
        string qsv,
        string amf)
    {
        // Список пресетов в настройках один на всех, а имён у кодировщиков видеокарты в нём
        // нет ни одного: таблица обязана покрывать все девять значений, иначе экспорт упадёт
        // на словаре в последнем кадре ролика.
        Assert.Equal(nvenc, HardwareEncoderOptions.Quality("nvenc", preset, 20).ToArray()[1]);
        Assert.Equal(qsv, HardwareEncoderOptions.Quality("qsv", preset, 20).ToArray()[1]);
        Assert.Equal(amf, HardwareEncoderOptions.Quality("amf", preset, 20).ToArray()[1]);
    }

    [Theory]
    [InlineData("libx264", "hevc_nvenc")]
    [InlineData("libx265", "h264_nvenc")]
    [InlineData("libx265", "h264_qsv")]
    [InlineData("libx265", "h264_amf")]
    public void КодировщикНеТогоКодека_Отклоняется(string codec, string encoder)
    {
        // hevc_nvenc при libx264 упал бы на первом кадре, и разбираться пришлось бы по словам
        // ffmpeg вместо внятного отказа здесь.
        var settings = Settings with { VideoCodec = codec };

        var exception = Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(settings, "аудио.wav", "итог.mp4", encoder));

        Assert.Contains(encoder, exception.Message, StringComparison.Ordinal);
        Assert.Contains(codec, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void НеизвестныйКодировщикВидеокарты_Отклоняется()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(Settings, "аудио.wav", "итог.mp4", "h264_mf"));

        Assert.Contains("h264_mf", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HevcНаВидеокарте_ТегДорожкиТотже()
    {
        // Тег ставится по кодеку, а не по кодировщику: hvc1 нужен и libx265, и hevc_nvenc.
        var arguments = FfmpegArguments.Build(Settings with { VideoCodec = "libx265" }, "аудио.wav", "итог.mp4", "hevc_nvenc").ToArray();

        AssertPair(arguments, "-tag:v", "hvc1");
    }

    [Fact]
    public void ПустойКодировщик_ОстаётсяПрограммным()
    {
        // Пустая строка - это не «кодируй неизвестным», а обычный программный путь: так его
        // передаёт выгрузка, когда в настройках выбрана видеокарта, но кодировщик не нашёлся.
        var arguments = FfmpegArguments.Build(Settings, "аудио.wav", "итог.mp4", string.Empty);

        Assert.Contains("libx264", arguments);
        Assert.Contains("-crf", arguments);
    }

    [Fact]
    public void НеизвестныйКодекВидео_Отклоняется()
    {
        var settings = Settings with { VideoCodec = "libvpx-vp9" };

        var exception = Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(settings, "аудио.wav", "итог.mp4"));

        Assert.Contains("libvpx-vp9", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ПустойКодекВидео_Отклоняется()
    {
        Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(Settings with { VideoCodec = " " }, "аудио.wav", "итог.mp4"));
    }

    [Fact]
    public void Build_ПоследнийАргумент_ПутьРезультата()
    {
        var arguments = Build("D:\\видео\\итог.mp4");

        Assert.Equal("D:\\видео\\итог.mp4", arguments[^1]);
    }

    [Fact]
    public void Build_ПерезаписываетБезВопроса()
    {
        // Без -y ffmpeg зависает на вопросе «перезаписать?» и ждёт ввода, которого не будет.
        Assert.Contains("-y", Build());
    }

    [Fact]
    public void Build_ОстанавливаетсяПоКороткойДорожке()
    {
        // Видео беззвучно не кончается: без -shortest ffmpeg ждал бы конца потока кадров.
        Assert.Contains("-shortest", Build());
    }

    [Theory]
    [InlineData(1281, 720)]
    [InlineData(1280, 721)]
    public void НечётныйРазмер_Отклоняется(int width, int height)
    {
        var settings = Settings with { Width = width, Height = height };

        var exception = Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(settings, "аудио.wav", "итог.mp4"));

        Assert.Contains("чётным", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void НеизвестныйПресет_Отклоняется()
    {
        var settings = Settings with { CodecPreset = "turbo" };

        var exception = Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(settings, "аудио.wav", "итог.mp4"));

        Assert.Contains("turbo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void НулеваяЧастотаКадров_Отклоняется()
    {
        var settings = Settings with { FramesPerSecond = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FfmpegArguments.Build(settings, "аудио.wav", "итог.mp4"));
    }

    [Fact]
    public void ПустойПуть_Отклоняется()
    {
        Assert.Throws<ArgumentException>(() => FfmpegArguments.Build(Settings, " ", "итог.mp4"));
        Assert.Throws<ArgumentException>(() => FfmpegArguments.Build(Settings, "аудио.wav", " "));
    }

    [Theory]
    [InlineData("nmr")]
    [InlineData("twoloop")]
    [InlineData("fast")]
    public void Build_КодировщикЗвукаСтоитРядомСAac(string coder)
    {
        // Кодировщик задаётся явно: nmr и есть значение ffmpeg по умолчанию, но на части
        // сигналов он зависает, и выбирать его должен пользователь, а не сборка ffmpeg.
        var arguments = FfmpegArguments.Build(Settings with { AacCoder = coder }, "аудио.wav", "итог.mp4").ToArray();
        var index = Array.IndexOf(arguments, "-aac_coder");

        Assert.True(index > 0, "в команде нет -aac_coder");
        Assert.Equal("aac", arguments[index - 1]);
        Assert.Equal(coder, arguments[index + 1]);
    }

    [Fact]
    public void Build_КодировщикЗвукаПриводитсяКНижнемуРегистру()
    {
        var arguments = FfmpegArguments.Build(Settings with { AacCoder = " TwoLoop " }, "аудио.wav", "итог.mp4").ToArray();

        Assert.Contains("twoloop", arguments);
    }

    [Fact]
    public void НеизвестныйКодировщикЗвука_Отклоняется()
    {
        var settings = Settings with { AacCoder = "lpc" };

        var exception = Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(settings, "аудио.wav", "итог.mp4"));

        Assert.Contains("lpc", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ПустойКодировщикЗвука_Отклоняется()
    {
        Assert.Throws<ArgumentException>(
            () => FfmpegArguments.Build(Settings with { AacCoder = " " }, "аудио.wav", "итог.mp4"));
    }

    [Fact]
    public void Build_ЧислаНеЗависятОтЯзыка()
    {
        // Культура с запятой в качестве разделителя дописала бы в -framerate "30,0000000000000",
        // и ffmpeg не разобрал бы параметр.
        var arguments = FfmpegArguments.Build(Settings, "аудио.wav", "итог.mp4");

        Assert.DoesNotContain(arguments, argument => argument.Contains(',', StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> Build(string outputPath = "итог.mp4")
        => FfmpegArguments.Build(Settings, "аудио.wav", outputPath);

    private static void AssertPair(IReadOnlyList<string> arguments, string key, string value)
    {
        var index = Array.IndexOf(arguments.ToArray(), key);

        Assert.True(index > 0, $"в команде нет {key}");
        Assert.Equal(value, arguments[index + 1]);
    }
}