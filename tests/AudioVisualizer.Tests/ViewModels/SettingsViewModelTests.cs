using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static SettingsViewModel Create(AppSettings? settings = null) => new(settings ?? AppSettings.Default);

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
            Bars = new BarSettings { Width = 14, CornerRadius = 7, BorderWidth = 2.5d },
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
    public void ВыборПресетaРазмера_ПроставляетРазмеры()
    {
        var viewModel = Create();

        viewModel.SelectedSizePreset = viewModel.SizePresets.First(choice => choice.Value == "1080p30");

        Assert.Equal(1920, viewModel.ExportWidth);
        Assert.Equal(1080, viewModel.ExportHeight);
        Assert.Equal(30, viewModel.ExportFramesPerSecond);
    }

    [Fact]
    public void ПресетРазмера_НеСбрасываетсяОтСвоихПолей()
    {
        var viewModel = Create();
        var preset = viewModel.SizePresets.First(choice => choice.Value == "720p30");

        viewModel.SelectedSizePreset = preset;

        Assert.Same(preset, viewModel.SelectedSizePreset);
    }

    [Fact]
    public void РучнаяПравкаРазмера_СбрасываетПресет()
    {
        var viewModel = Create();
        viewModel.SelectedSizePreset = viewModel.SizePresets.First(choice => choice.Value == "1080p30");

        viewModel.ExportWidth = 1600;

        Assert.Null(viewModel.SelectedSizePreset);
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
        Assert.Equal(3, viewModel.CornerStyleChoices.Count);
        Assert.Equal(2, viewModel.FillModeChoices.Count);
        Assert.Equal(3, viewModel.SizePresets.Count);
    }
}