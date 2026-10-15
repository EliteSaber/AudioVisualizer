using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Settings;

public class AppSettingsStoreTests
{
    private static AppSettings Sample() => new()
    {
        Colors = new ColorSettings
        {
            Background = new RgbaColor(1, 2, 3, 4),
            BarFill = new RgbaColor(10, 20, 30, 255),
            CenterLine = RgbaColor.Transparent
        },
        Bars = new BarSettings { Width = 12, Spacing = 6, CornerStyle = CornerStyle.Rounded, FillMode = BarFillMode.Solid },
        Spectrum = new SpectrumSettings { FftSize = 2048, AnalysisBandCount = 128, GainDb = 3f },
        Export = new ExportSettings { Width = 1280, Height = 720, FramesPerSecond = 30, CodecPreset = "veryfast" },
        QuietExport = true
    };

    [Fact]
    public void Load_БезФайла_СоздаётФайлИВозвращаетУмолчания()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("settings.json");
        var store = new AppSettingsStore(path);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveИLoad_СохраняютНастройки()
    {
        using var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(directory.File("settings.json"));
        var expected = Sample();

        Assert.True(store.Save(expected));

        Assert.Equal(expected, store.Load());
    }

    [Fact]
    public void Load_БитыйJson_ВозвращаетУмолчания_ИНеПереписываетФайл()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("settings.json");
        File.WriteAllText(path, "{ это не json");
        var store = new AppSettingsStore(path);

        var settings = store.Load();

        Assert.Equal(AppSettings.Default, settings);
        Assert.Equal("{ это не json", File.ReadAllText(path));
    }

    [Fact]
    public void Load_ЧастичныйJson_ДобираетЗначенияПоУмолчанию()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("settings.json");
        File.WriteAllText(path, "{ \"Bars\": { \"Width\": 16 } }");
        var store = new AppSettingsStore(path);

        var settings = store.Load();

        Assert.Equal(16, settings.Bars.Width);
        Assert.Equal(AppSettings.Default.Bars.Spacing, settings.Bars.Spacing);
        Assert.Equal(AppSettings.Default.Export, settings.Export);
    }

    [Fact]
    public void Load_НегодныеЗначения_Нормализует()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("settings.json");
        File.WriteAllText(path, "{ \"Export\": { \"Width\": 1921, \"FramesPerSecond\": 1000, \"Crf\": 100 } }");
        var store = new AppSettingsStore(path);

        var settings = store.Load();

        Assert.Equal(1920, settings.Export.Width);
        Assert.Equal(240, settings.Export.FramesPerSecond);
        Assert.Equal(51, settings.Export.Crf);
    }

    [Fact]
    public void Load_ЯвныеNull_ВозвращаетУмолчания()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("settings.json");
        File.WriteAllText(path, "{ \"Colors\": null, \"Bars\": null, \"Spectrum\": null, \"Export\": null }");
        var store = new AppSettingsStore(path);

        Assert.Equal(AppSettings.Default, store.Load());
    }

    [Fact]
    public void Save_ВНесуществующийКаталог_ВозвращаетFalse()
    {
        using var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(Path.Combine(directory.Root, "нет-такого", "settings.json"));

        Assert.False(store.Save(AppSettings.Default));
    }

    [Fact]
    public void Save_ОшибкаНеПробрасывается()
    {
        using var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(directory.File("settings.json"));

        Assert.True(store.Save(AppSettings.Default));
        Assert.True(store.Save(Sample()));
    }

    [Fact]
    public void Resolve_УказываетНаФайлРядомСПрограммой()
    {
        var path = AppSettingsStore.Resolve();

        Assert.Equal("settings.json", Path.GetFileName(path));
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetDirectoryName(path)!)));
    }

    [Fact]
    public void FilePath_ХранитПереданныйПуть()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("my.json");

        Assert.Equal(path, new AppSettingsStore(path).FilePath);
    }
}