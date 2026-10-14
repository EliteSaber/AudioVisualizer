using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Settings;

public class PresetStoreTests
{
    private static AppSettings Sample() => new()
    {
        Bars = new BarSettings { Width = 14, CornerRadius = 7 },
        Colors = new ColorSettings { Background = new RgbaColor(9, 8, 7, 6) }
    };

    [Fact]
    public void SaveИLoad_СохраняютНастройки()
    {
        using var directory = new TemporaryDirectory();
        var store = new PresetStore(directory.Root);
        var expected = Sample();

        store.Save("Неон", expected);

        Assert.Equal(expected, store.Load("Неон"));
    }

    [Fact]
    public void List_ВозвращаетИменаПоАлфавиту()
    {
        using var directory = new TemporaryDirectory();
        var store = new PresetStore(directory.Root);
        store.Save("Яркий", AppSettings.Default);
        store.Save("Аква", AppSettings.Default);

        Assert.Equal(["Аква", "Яркий"], store.List());
    }

    [Fact]
    public void List_БезКаталога_Пуст()
    {
        using var directory = new TemporaryDirectory();

        Assert.Empty(new PresetStore(Path.Combine(directory.Root, "нет-такого")).List());
    }

    [Fact]
    public void Load_НесуществующийПресет_ВыбрасываетFileNotFound()
    {
        using var directory = new TemporaryDirectory();
        var store = new PresetStore(directory.Root);

        Assert.Throws<FileNotFoundException>(() => store.Load("НетТакого"));
    }

    [Fact]
    public void Delete_УдаляетФайл()
    {
        using var directory = new TemporaryDirectory();
        var store = new PresetStore(directory.Root);
        store.Save("Кислотный", AppSettings.Default);

        Assert.True(store.Delete("Кислотный"));
        Assert.Empty(store.List());
        Assert.False(store.Delete("Кислотный"));
    }

    [Fact]
    public void Sanitize_УбираетНедопустимыеСимволы()
    {
        var sanitized = PresetStore.Sanitize("  ди/ло*го?  ");

        Assert.Equal("ди_ло_го_", sanitized);
    }

    [Fact]
    public void Sanitize_ПустоеИмя_ВыбрасываетArgumentException()
        => Assert.Throws<ArgumentException>(() => PresetStore.Sanitize("   "));

    [Fact]
    public void Sanitize_НедопустимоеИмя_НеПозволяетУйтиИзКаталога()
    {
        using var directory = new TemporaryDirectory();
        var store = new PresetStore(directory.Root);

        store.Save("../побег", AppSettings.Default);

        var files = Directory.GetFiles(directory.Root, "*", SearchOption.AllDirectories);
        Assert.All(files, file => Assert.StartsWith(Path.GetFullPath(directory.Root), Path.GetFullPath(file)));
        Assert.Contains(files, file => Path.GetFileNameWithoutExtension(file).Contains("побег", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_УказываетНаКаталогРядомСПрограммой()
    {
        Assert.Equal(PresetStore.DirectoryName, Path.GetFileName(PresetStore.Resolve()));
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetDirectoryName(PresetStore.Resolve())!)));
    }
}