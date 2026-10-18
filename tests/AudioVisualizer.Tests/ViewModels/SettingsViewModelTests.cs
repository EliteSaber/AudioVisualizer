using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Export;

namespace AudioVisualizer.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static readonly string PresetsPath = Path.Combine(Path.GetTempPath(), "av-test-exportpresets.json");

    /// <summary>
    /// У каждого теста свой пустой файл пресетов: иначе пресет, сохранённый одним тестом,
    /// попадал бы в список другого, а результат зависел от порядка запуска.
    /// </summary>
    /// <param name="settings">Настройки для панели.</param>
    /// <param name="detector">
    /// Зонд видеокарты. Без него панель искала бы настоящий ffmpeg и запускала пробы: тесты
    /// модели представления не должны зависеть от того, что стоит на машине.
    /// </param>
    private static SettingsViewModel Create(
        AppSettings? settings = null,
        IVideoEncoderDetector? detector = null)
    {
        if (File.Exists(PresetsPath))
        {
            File.Delete(PresetsPath);
        }

        return new SettingsViewModel(
            settings ?? AppSettings.Default,
            new ExportPresetStore(PresetsPath),
            detector ?? new FakeEncoderDetector([]));
    }

    [Fact]
    public void Конструктор_РазбираетНастройки_НаПоля()
    {
        var settings = AppSettings.Default with
        {
            Bars = new BarSettings { Width = 17, CornerRadius = 5, CornerStyle = CornerStyle.Rectangular },
            Colors = new ColorSettings { Background = new RgbaColor(1, 2, 3, 4) },
            Export = new ExportSettings { Width = 1280, Height = 720, FramesPerSecond = 30 }
        };

        var viewModel = Create(settings);

        Assert.Equal(17, viewModel.BarWidth);
        Assert.Equal(CornerStyle.Rectangular, viewModel.BarCornerStyle);
        Assert.Equal(new RgbaColor(1, 2, 3, 4), viewModel.BackgroundColor);
        Assert.Equal(1280, viewModel.ExportWidth);
        Assert.Equal(30, viewModel.ExportFramesPerSecond);
    }

    [Fact]
    public void ToAppSettings_ВозвращаетТеЖеЗначения()
    {
        var source = SettingsNormalizer.Normalize(AppSettings.Default with
        {
            Spectrum = new SpectrumSettings { Gamma = 0.7f, GainDb = 4f },
            Bars = new BarSettings { Width = 14, CornerRadius = 7, BorderWidth = 2.5d, TrailDecayMs = 240d, TrailDensity = 0.6d },
            Export = new ExportSettings { Width = 1280, Height = 720, FramesPerSecond = 30 }
        });

        var settings = Create(source).ToAppSettings();

        Assert.Equal(source.Colors, settings.Colors);
        Assert.Equal(source.Bars, settings.Bars);
        Assert.Equal(source.Export, settings.Export);
        Assert.Equal(source.Spectrum.FftSize, settings.Spectrum.FftSize);
        Assert.Equal(source.Spectrum.MinFrequencyHz, settings.Spectrum.MinFrequencyHz);
        Assert.Equal(source.Spectrum.MaxFrequencyHz, settings.Spectrum.MaxFrequencyHz);
        Assert.Equal(source.Spectrum.MinDecibels, settings.Spectrum.MinDecibels);
        Assert.Equal(source.Spectrum.MaxDecibels, settings.Spectrum.MaxDecibels);
        Assert.Equal(source.Spectrum.GainDb, settings.Spectrum.GainDb);
        Assert.Equal(source.Spectrum.Gamma, settings.Spectrum.Gamma);
        Assert.Equal(source.Spectrum.Attack, settings.Spectrum.Attack);
        Assert.Equal(source.Spectrum.Release, settings.Spectrum.Release);
        Assert.Equal(source.Spectrum.Equalizer, settings.Spectrum.Equalizer);
    }

    [Fact]
    public void ИзменениеПоля_ПоднимаетChanged()
    {
        var viewModel = Create();
        var raised = 0;
        viewModel.Changed += (_, _) => raised++;

        viewModel.BarWidth = 12;

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Apply_ПоднимаетChanged_ОдинРаз()
    {
        var viewModel = Create();
        var raised = 0;
        viewModel.Changed += (_, _) => raised++;

        viewModel.Apply(AppSettings.Default);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ВыборПресетa_ПроставляетПараметрыРолика()
    {
        var viewModel = Create();

        viewModel.SelectedExportPreset = viewModel.ExportPresets.First(preset => preset.Name == "720p30");

        Assert.Equal(1280, viewModel.ExportWidth);
        Assert.Equal(720, viewModel.ExportHeight);
        Assert.Equal(30, viewModel.ExportFramesPerSecond);
        Assert.Equal(ExportSettings.DefaultVideoCodec, viewModel.VideoCodec);
        Assert.Equal(18, viewModel.Crf);
        Assert.Equal("medium", viewModel.CodecPreset);
        Assert.Equal(192, viewModel.AudioBitrateKbps);
        Assert.Equal("nmr", viewModel.AacCoder);
    }

    [Fact]
    public void КодировщикЗвука_ДоезжаетВНастройкиИОбратно()
    {
        // Значение обязано дойти до команды ffmpeg: без него панель настраивает то,
        // чего выгрузка не спрашивает.
        var viewModel = Create();

        viewModel.AacCoder = "twoloop";
        Assert.Equal("twoloop", viewModel.ToAppSettings().Export.AacCoder);

        viewModel.Apply(AppSettings.Default with { Export = new ExportSettings { AacCoder = "fast" } });
        Assert.Equal("fast", viewModel.AacCoder);
    }

    [Fact]
    public void КодировщикЗвука_ПоУмолчаниюИзНастроек()
    {
        var viewModel = Create();

        Assert.Equal(ExportSettings.DefaultAacCoder, viewModel.AacCoder);
        Assert.Equal(viewModel.AacCoder, viewModel.AacCoderChoices[0]);
        Assert.Contains(viewModel.AacCoder, viewModel.AacCoderChoices);
    }

    [Fact]
    public void КодекВидео_ДоезжаетВНастройкиИОбратно()
    {
        // Значение обязано дойти до команды ffmpeg: иначе панель настраивает то,
        // чего выгрузка не спрашивает.
        var viewModel = Create();

        viewModel.VideoCodec = "libx265";
        Assert.Equal("libx265", viewModel.ToAppSettings().Export.VideoCodec);

        viewModel.Apply(AppSettings.Default with { Export = new ExportSettings { VideoCodec = "libx265" } });
        Assert.Equal("libx265", viewModel.VideoCodec);
    }

    [Fact]
    public void КодекВидео_ПоУмолчаниюИзНастроек()
    {
        var viewModel = Create();

        Assert.Equal(ExportSettings.DefaultVideoCodec, viewModel.VideoCodec);
        Assert.Contains(viewModel.VideoCodec, viewModel.VideoCodecChoices.Select(choice => choice.Value));
    }

    [Fact]
    public void ТихаяВыгрузка_ДоезжаетВНастройкиИОбратно()
    {
        var viewModel = Create();

        // Галочка лежит в AppSettings, а не в ExportSettings: тот уезжает в пресеты ролика,
        // а это свойство машины, и в пресете ему не место.
        viewModel.QuietExport = true;
        Assert.True(viewModel.ToAppSettings().QuietExport);

        viewModel.Apply(AppSettings.Default);
        Assert.False(viewModel.QuietExport);
    }

    [Fact]
    public void ВыборПресетa_НеСбрасываетсяОтСвоихПолей()
    {
        var viewModel = Create();
        var preset = viewModel.ExportPresets.First(preset => preset.Name == "720p30");

        viewModel.SelectedExportPreset = preset;

        Assert.Same(preset, viewModel.SelectedExportPreset);
    }

    [Fact]
    public void РучнаяПравкаРазмера_СбрасываетПресет()
    {
        var viewModel = Create();
        viewModel.SelectedExportPreset = viewModel.ExportPresets.First(preset => preset.Name == "1080p30");

        viewModel.ExportWidth = 1600;

        Assert.Null(viewModel.SelectedExportPreset);
    }

    [Theory]
    [InlineData("ExportFramesPerSecond")]
    [InlineData("VideoCodec")]
    [InlineData("Crf")]
    [InlineData("CodecPreset")]
    [InlineData("AudioBitrateKbps")]
    [InlineData("AacCoder")]
    public void РучнаяПравкаПараметраРолика_СбрасываетПресет(string propertyName)
    {
        var viewModel = Create();
        viewModel.SelectedExportPreset = viewModel.ExportPresets.First(preset => preset.Name == "1080p30");

        typeof(SettingsViewModel).GetProperty(propertyName)!.SetValue(
            viewModel,
            propertyName switch
            {
                "ExportFramesPerSecond" => 24,
                "VideoCodec" => "libx265",
                "Crf" => 20,
                "CodecPreset" => "fast",
                "AacCoder" => "twoloop",
                _ => 256
            });

        Assert.Null(viewModel.SelectedExportPreset);
    }

    [Fact]
    public void ПравкаПутиКFfmpeg_НеСбрасываетПресет()
    {
        // Путь к ffmpeg в пресет не входит, поэтому правка пути не делает набор неверным.
        var viewModel = Create();
        var preset = viewModel.ExportPresets.First(preset => preset.Name == "1080p30");
        viewModel.SelectedExportPreset = preset;

        viewModel.FfmpegPath = @"C:\tools\ffmpeg.exe";

        Assert.Same(preset, viewModel.SelectedExportPreset);
    }

    [Fact]
    public void Equalizer_СоздаётПолосыИзНастроек()
    {
        var settings = AppSettings.Default with
        {
            Spectrum = new SpectrumSettings { Equalizer = [new EqualizerBand(100f, 3f), new EqualizerBand(1000f, -6f)] }
        };

        var viewModel = Create(settings);

        Assert.Equal(2, viewModel.Equalizer.Count);
        Assert.Equal("100 Гц", viewModel.Equalizer[0].Title);
        Assert.Equal("1 кГц", viewModel.Equalizer[1].Title);
        Assert.Equal(3f, viewModel.Equalizer[0].GainDb);
    }

    [Fact]
    public void ИзменениеУсиленияПолосы_ПоднимаетChanged()
    {
        var viewModel = Create();
        var raised = 0;
        viewModel.Changed += (_, _) => raised++;

        viewModel.Equalizer[0].GainDb = 5f;

        Assert.Equal(1, raised);
        Assert.Equal(5f, viewModel.ToAppSettings().Spectrum.Equalizer[0].GainDb);
    }

    [Fact]
    public void ПустойПутьКFfmpeg_СтановитсяПустымНеNull()
    {
        var viewModel = Create(AppSettings.Default with { Export = new ExportSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" } });

        Assert.Equal(@"C:\tools\ffmpeg.exe", viewModel.FfmpegPath);
    }

    [Fact]
    public void Конструктор_БезНастроек_Отклоняется()
        => Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!));

    [Fact]
    public void СпискиВыбора_НеПустые()
    {
        var viewModel = Create();

        Assert.NotEmpty(viewModel.CodecPresets);
        Assert.Equal(2, viewModel.VideoCodecChoices.Count);
        Assert.All(viewModel.VideoCodecChoices, choice => Assert.Contains(choice.Value, ExportSettings.VideoCodecs));
        Assert.Equal(3, viewModel.CornerStyleChoices.Count);
        Assert.Equal(2, viewModel.FillModeChoices.Count);
        Assert.Equal(3, viewModel.ExportPresets.Count);
        Assert.All(viewModel.ExportPresets, preset => Assert.False(preset.Custom));
    }

    [Fact]
    public void КодированиеВидео_ПоУмолчаниюПроцессор()
    {
        var viewModel = Create();

        Assert.Equal(ExportSettings.DefaultVideoEncoder, viewModel.VideoEncoder);
        Assert.Contains(viewModel.VideoEncoder, viewModel.EncoderChoices.Select(choice => choice.Value));
    }

    [Fact]
    public async Task НайденнаяВидеокарта_ПопадаетВСписок()
    {
        var viewModel = Create(detector: new FakeEncoderDetector([new VideoEncoderSupport("nvenc", "NVIDIA", "h264_nvenc")]));

        await viewModel.RefreshEncodersAsync();

        Assert.Contains(viewModel.EncoderChoices, choice => choice.Value == "nvenc");
        Assert.Equal("NVIDIA", viewModel.EncoderChoices.Single(choice => choice.Value == "nvenc").ToString());
        Assert.Equal(string.Empty, viewModel.EncodersMessage);
    }

    [Fact]
    public async Task БезВидеокарты_ВСпискеОстаютсяДваПунктаИОбъяснение()
    {
        // Молча оставить один пункт значило бы выглядеть так, будто видеокарты не бывает.
        var viewModel = Create(detector: new FakeEncoderDetector([]));

        await viewModel.RefreshEncodersAsync();

        Assert.Equal(2, viewModel.EncoderChoices.Count);
        Assert.Contains("видеокарта", viewModel.EncodersMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task БезFfmpeg_ПанельГоворитПроFfmpegАНеПроВидеокарту()
    {
        // Причина важна: потерявшийся из PATH ffmpeg выглядел бы как отсутствие видеокарты,
        // и человек пошёл бы искать не туда.
        var viewModel = Create(detector: new FakeEncoderDetector([], ffmpegAvailable: false));

        await viewModel.RefreshEncodersAsync();

        Assert.Contains("ffmpeg", viewModel.EncodersMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("видеокарта", viewModel.EncodersMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ЕстьВидеокарта_ОбъясненияНет()
    {
        var viewModel = Create(
            detector: new FakeEncoderDetector([new VideoEncoderSupport("nvenc", "NVIDIA", "h264_nvenc")]));

        await viewModel.RefreshEncodersAsync();

        Assert.Equal(string.Empty, viewModel.EncodersMessage);
    }

    [Fact]
    public async Task ВыбраннаяНайденнаяВидеокарта_ОстаётсяПриСменеКодека()
    {
        // Зонд бьёт по коду: сменился кодек - список мог сжаться, и выбор не должен слететь.
        var viewModel = Create(
            AppSettings.Default with { Export = new ExportSettings { VideoEncoder = "nvenc" } },
            new FakeEncoderDetector([new VideoEncoderSupport("nvenc", "NVIDIA", "h264_nvenc")]));

        viewModel.VideoCodec = "libx265";
        await viewModel.RefreshEncodersAsync();

        Assert.Equal("nvenc", viewModel.VideoEncoder);
        Assert.Contains(viewModel.EncoderChoices, choice => choice.Value == "nvenc");
    }

    [Fact]
    public async Task ВыбраннойВидеокартыНет_ОнаОстаётсяВСпискеСПометкой()
    {
        // Сбрасывать выбор молча нельзя: пользователь вернулся из поездки с другой видеокартой
        // и должен видеть, почему список стал короче.
        var viewModel = Create(AppSettings.Default with { Export = new ExportSettings { VideoEncoder = "amf" } });

        await viewModel.RefreshEncodersAsync();

        Assert.Equal("amf", viewModel.VideoEncoder);
        Assert.Contains("amf", viewModel.EncoderChoices.Select(choice => choice.Value));
        Assert.Contains(
            viewModel.EncoderChoices,
            choice => choice.Value == "amf" && choice.ToString().Contains("не найдена", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task КодированиеВидео_ДоезжаетВНастройкиИОбратно()
    {
        var viewModel = Create(AppSettings.Default with { Export = new ExportSettings { VideoEncoder = "auto" } });

        Assert.Equal("auto", viewModel.VideoEncoder);
        Assert.Equal("auto", viewModel.ToAppSettings().Export.VideoEncoder);

        viewModel.VideoEncoder = "qsv";
        Assert.Equal("qsv", viewModel.ToAppSettings().Export.VideoEncoder);
    }

[Fact]
    public async Task ПравкаПутиКFfmpeg_ПерезапускаетПробуСама()
    {
        // Путь задаётся один на машину, и после его правки прежний ответ зонда больше не годен.
        // Зонд дёргается сам, звать RefreshEncodersAsync вручную не нужно.
        var detector = new FakeEncoderDetector([]);
        var viewModel = Create(detector: detector);

        await viewModel.RefreshEncodersAsync();
        Assert.Equal(1, detector.Calls);

        viewModel.FfmpegPath = @"C:\tools\ffmpeg.exe";

        Assert.Equal(2, detector.Calls);
    }

    [Fact]
    public async Task ПрограммнаяПравкаНастроек_ЗондНеДёргает()
    {
        // Apply и выбор пресета меняют поля настроек, а не выбор пользователя: зонд на каждый
        // пересозданный пресет запускать пробы - это десятки миллисекунд впустую.
        var detector = new FakeEncoderDetector([]);
        var viewModel = Create(detector: detector);

        await viewModel.RefreshEncodersAsync();
        viewModel.Apply(AppSettings.Default with { Export = new ExportSettings { VideoCodec = "libx265" } });

        Assert.Equal(1, detector.Calls);
    }

    [Fact]
    public async Task ОшибкаЗонда_НеРоняетПанель()
    {
        var viewModel = Create(detector: new FakeEncoderDetector(new InvalidOperationException("ffmpeg сломан")));

        await viewModel.RefreshEncodersAsync();

        Assert.Equal(2, viewModel.EncoderChoices.Count);
        Assert.Equal(ExportSettings.DefaultVideoEncoder, viewModel.VideoEncoder);
        Assert.False(viewModel.IsDetectingEncoders);
    }
}