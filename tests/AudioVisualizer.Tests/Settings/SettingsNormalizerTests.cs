using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Settings;

public class SettingsNormalizerTests
{
    [Fact]
    public void Normalize_КорректныеНастройки_ОстаютсяПрежними()
    {
        var normalized = SettingsNormalizer.Normalize(AppSettings.Default);

        Assert.Equal(AppSettings.Default, normalized);
    }

    [Fact]
    public void Normalize_ШиринаСтолбика_Ограничивается()
    {
        var bars = SettingsNormalizer.NormalizeBars(new BarSettings { Width = 0 });
        Assert.Equal(1, bars.Width);

        bars = SettingsNormalizer.NormalizeBars(new BarSettings { Width = 5000 });
        Assert.Equal(200, bars.Width);
    }

    [Fact]
    public void Normalize_РадиусСкругления_НеБольшеПоловиныШирины()
    {
        var bars = SettingsNormalizer.NormalizeBars(new BarSettings { Width = 10, CornerRadius = 40 });

        Assert.Equal(5, bars.CornerRadius);
    }

    [Fact]
    public void Normalize_ПромежутокОтступЗазорГраница_Ограничиваются()
    {
        var bars = SettingsNormalizer.NormalizeBars(new BarSettings
        {
            Spacing = -5,
            Margin = -10,
            CenterGap = 9999,
            BorderWidth = -1d
        });

        Assert.Equal(0, bars.Spacing);
        Assert.Equal(0, bars.Margin);
        Assert.Equal(500, bars.CenterGap);
        Assert.Equal(0d, bars.BorderWidth);
    }

    [Fact]
    public void Normalize_ВремяСледа_Ограничивается()
    {
        Assert.Equal(0d, SettingsNormalizer.NormalizeBars(new BarSettings { TrailDecayMs = -100d }).TrailDecayMs);
        Assert.Equal(5000d, SettingsNormalizer.NormalizeBars(new BarSettings { TrailDecayMs = 999_999d }).TrailDecayMs);
    }

    [Fact]
    public void Normalize_ПлотностьСледа_Ограничивается()
    {
        Assert.Equal(0.8d, SettingsNormalizer.NormalizeBars(new BarSettings()).TrailDensity);
        Assert.Equal(0d, SettingsNormalizer.NormalizeBars(new BarSettings { TrailDensity = -0.5d }).TrailDensity);
        Assert.Equal(1d, SettingsNormalizer.NormalizeBars(new BarSettings { TrailDensity = 3d }).TrailDensity);
    }

[Fact]
    public void Normalize_НеизвестныйВидСкругления_ЗаменяетсяНаУмолчание()
    {
        var bars = SettingsNormalizer.NormalizeBars(new BarSettings { CornerStyle = (CornerStyle)99 });

        Assert.Equal(CornerStyle.OuterRounded, bars.CornerStyle);
    }

    [Fact]
    public void Normalize_FftSize_ОкругляетсяДоСтепениДвойки()
    {
        var spectrum = SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings { FftSize = 3000 });

        Assert.Equal(4096, spectrum.FftSize);
    }

    [Fact]
    public void Normalize_FftSize_ОграничиваетсяСверху()
    {
        var spectrum = SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings { FftSize = 1_000_000 });

        Assert.Equal(SettingsNormalizer.MaximumFftSize, spectrum.FftSize);
    }

    [Fact]
    public void Normalize_ЧислоПолос_Ограничивается()
    {
        Assert.Equal(8, SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings { AnalysisBandCount = 1 }).AnalysisBandCount);
        Assert.Equal(1024, SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings { AnalysisBandCount = 100_000 }).AnalysisBandCount);
    }

    [Fact]
    public void Normalize_Частоты_СтавятсяВПорядке()
    {
        var spectrum = SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings
        {
            MinFrequencyHz = 5000f,
            MaxFrequencyHz = 1000f
        });

        Assert.True(spectrum.MaxFrequencyHz > spectrum.MinFrequencyHz);
    }

    [Fact]
    public void Normalize_Частоты_ОграничиваютсяСверху()
    {
        var spectrum = SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings
        {
            MinFrequencyHz = 100_000f,
            MaxFrequencyHz = 200_000f
        });

        Assert.InRange(spectrum.MinFrequencyHz, SettingsNormalizer.MinimumFrequencyHz, SettingsNormalizer.MaximumFrequencyHz);
        Assert.InRange(spectrum.MaxFrequencyHz, spectrum.MinFrequencyHz, SettingsNormalizer.MaximumFrequencyHz);
    }

    [Fact]
    public void Normalize_Децибелы_СтавятсяВПорядке()
    {
        var spectrum = SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings
        {
            MinDecibels = -10f,
            MaxDecibels = -70f
        });

        Assert.True(spectrum.MaxDecibels > spectrum.MinDecibels);
    }

    [Fact]
    public void Normalize_ГаммаИСглаживание_Ограничиваются()
    {
        var spectrum = SettingsNormalizer.NormalizeSpectrum(new SpectrumSettings
        {
            Gamma = 50f,
            Attack = -3f,
            Release = 8f
        });

        Assert.Equal(4f, spectrum.Gamma);
        Assert.Equal(0f, spectrum.Attack);
        Assert.Equal(1f, spectrum.Release);
    }

    [Fact]
    public void Normalize_ПолосыЭквалайзера_СортируютсяИОграничиваются()
    {
        EqualizerBand[] bands = [new(4000f, 99f), new(1000f, -99f), new(2000f, 3f)];

        var normalized = SettingsNormalizer.NormalizeEqualizer(bands);

        Assert.Equal([1000f, 2000f, 4000f], normalized.Select(band => band.CenterFrequencyHz).ToArray());
        Assert.All(normalized, band => Assert.InRange(band.GainDb, -SettingsNormalizer.EqualizerGainLimitDb, SettingsNormalizer.EqualizerGainLimitDb));
    }

    [Fact]
    public void Normalize_ПустойЭквалайзер_БерётПолосыПоУмолчанию()
    {
        Assert.Equal(SpectrumSettings.DefaultEqualizer.Count, SettingsNormalizer.NormalizeEqualizer(null).Count);
        Assert.Equal(SpectrumSettings.DefaultEqualizer.Count, SettingsNormalizer.NormalizeEqualizer([]).Count);
    }

    [Fact]
    public void Normalize_НечётныйРазмерКадра_ДелаетсяЧётным()
    {
        var export = SettingsNormalizer.NormalizeExport(new ExportSettings { Width = 1921, Height = 1081 });

        Assert.Equal(1920, export.Width);
        Assert.Equal(1080, export.Height);
    }

    [Fact]
    public void Normalize_РазмерыКадра_Ограничиваются()
    {
        var export = SettingsNormalizer.NormalizeExport(new ExportSettings { Width = 10, Height = 99999 });

        Assert.Equal(64, export.Width);
        Assert.Equal(7680, export.Height);
    }

    [Fact]
    public void Normalize_ЧастотаКадровИКачество_Ограничиваются()
    {
        Assert.Equal(240, SettingsNormalizer.NormalizeExport(new ExportSettings { FramesPerSecond = 1000 }).FramesPerSecond);
        Assert.Equal(1, SettingsNormalizer.NormalizeExport(new ExportSettings { FramesPerSecond = 0 }).FramesPerSecond);
        Assert.Equal(51, SettingsNormalizer.NormalizeExport(new ExportSettings { Crf = 100 }).Crf);
        Assert.Equal(32, SettingsNormalizer.NormalizeExport(new ExportSettings { AudioBitrateKbps = 1 }).AudioBitrateKbps);
    }

    [Fact]
    public void Normalize_ПресетКодека_ПриводитсяКИзвестному()
    {
        Assert.Equal("veryfast", SettingsNormalizer.NormalizeExport(new ExportSettings { CodecPreset = " VeryFast " }).CodecPreset);
        Assert.Equal("medium", SettingsNormalizer.NormalizeExport(new ExportSettings { CodecPreset = "turbo" }).CodecPreset);
        Assert.Equal("medium", SettingsNormalizer.NormalizeExport(new ExportSettings { CodecPreset = null! }).CodecPreset);
    }

    [Fact]
    public void Normalize_КодировщикЗвука_ПриводитсяКИзвестному()
    {
        Assert.Equal("twoloop", SettingsNormalizer.NormalizeExport(new ExportSettings { AacCoder = " TwoLoop " }).AacCoder);
        Assert.Equal("nmr", SettingsNormalizer.NormalizeExport(new ExportSettings { AacCoder = "lpc" }).AacCoder);
        Assert.Equal("nmr", SettingsNormalizer.NormalizeExport(new ExportSettings { AacCoder = null! }).AacCoder);
    }

    [Theory]
    [InlineData("nmr")]
    [InlineData("twoloop")]
    [InlineData("fast")]
    public void Normalize_КодировщикЗвука_КаждыйИзСпискаПроходит(string coder)
    {
        // Список один на настройки, команду и панель, и он не должен обрезаться по дороге:
        // значение из настроек обязано дойти до команды ffmpeg без замены.
        Assert.Contains(coder, ExportSettings.AacCoders);
        Assert.Equal(coder, SettingsNormalizer.NormalizeExport(new ExportSettings { AacCoder = coder }).AacCoder);
    }

    [Fact]
    public void Normalize_КодекВидео_ПриводитсяКИзвестному()
    {
        Assert.Equal("libx265", SettingsNormalizer.NormalizeExport(new ExportSettings { VideoCodec = " LIBX265 " }).VideoCodec);
        Assert.Equal("libx264", SettingsNormalizer.NormalizeExport(new ExportSettings { VideoCodec = "libvpx-vp9" }).VideoCodec);
        Assert.Equal("libx264", SettingsNormalizer.NormalizeExport(new ExportSettings { VideoCodec = null! }).VideoCodec);
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    public void Normalize_КодекВидео_КаждыйИзСпискаПроходит(string codec)
    {
        Assert.Contains(codec, ExportSettings.VideoCodecs);
        Assert.Equal(codec, SettingsNormalizer.NormalizeExport(new ExportSettings { VideoCodec = codec }).VideoCodec);
    }

    [Fact]
    public void Normalize_КодированиеВидео_ПриводитсяКИзвестному()
    {
        Assert.Equal("nvenc", SettingsNormalizer.NormalizeExport(new ExportSettings { VideoEncoder = " NVENC " }).VideoEncoder);
        Assert.Equal("software", SettingsNormalizer.NormalizeExport(new ExportSettings { VideoEncoder = "vaapi" }).VideoEncoder);
        Assert.Equal("software", SettingsNormalizer.NormalizeExport(new ExportSettings { VideoEncoder = null! }).VideoEncoder);
    }

    [Theory]
    [InlineData("software")]
    [InlineData("auto")]
    [InlineData("nvenc")]
    [InlineData("qsv")]
    [InlineData("amf")]
    public void Normalize_КодированиеВидео_КаждоеИзСпискаПроходит(string encoder)
    {
        Assert.Contains(encoder, ExportSettings.VideoEncoders);
        Assert.Equal(encoder, SettingsNormalizer.NormalizeExport(new ExportSettings { VideoEncoder = encoder }).VideoEncoder);
    }

    [Fact]
    public void Normalize_ПутьКFfmpeg_ПустоеЗначениеСтановитсяNull()
    {
        Assert.Null(SettingsNormalizer.NormalizeExport(new ExportSettings { FfmpegPath = "   " }).FfmpegPath);
        Assert.Equal(@"C:\tools\ffmpeg.exe", SettingsNormalizer.NormalizeExport(new ExportSettings { FfmpegPath = " C:\\tools\\ffmpeg.exe " }).FfmpegPath);
    }

    [Fact]
    public void Normalize_Громкость_Ограничивается()
    {
        Assert.Equal(1d, SettingsNormalizer.Normalize(AppSettings.Default with { Volume = 5d }).Volume);
        Assert.Equal(0d, SettingsNormalizer.Normalize(AppSettings.Default with { Volume = -2d }).Volume);
        Assert.Equal(0.4d, SettingsNormalizer.Normalize(AppSettings.Default with { Volume = 0.4d }).Volume);
    }

    [Fact]
    public void Normalize_Громкость_НеЧислоСтановитсяПолной()
    {
        // Math.Clamp на NaN возвращает NaN, а сравнения с ним ложны: значение проходит
        // насквозь. Нормализатор - страховка файла, и нечисло должно становиться полной
        // громкостью, а не NaN в плеере.
        Assert.Equal(1d, SettingsNormalizer.Normalize(AppSettings.Default with { Volume = double.NaN }).Volume);
        Assert.Equal(1d, SettingsNormalizer.Normalize(AppSettings.Default with { Volume = double.PositiveInfinity }).Volume);
        Assert.Equal(1d, SettingsNormalizer.Normalize(AppSettings.Default with { Volume = double.NegativeInfinity }).Volume);
    }
}