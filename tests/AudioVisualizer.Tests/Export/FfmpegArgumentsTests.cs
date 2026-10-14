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