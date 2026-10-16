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
}