using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Settings;

/// <summary>
/// Хранилище пользовательских пресетов. Файл принадлежит копии программы и правится руками,
/// поэтому проверяется не только круг записи, но и поведение при испорченном файле.
/// </summary>
public class ExportPresetStoreTests
{
    [Fact]
    public void СохранениеИЧтение_ПресетВозвращается()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        store.Save([new ExportPreset { Name = "Мой рендер", Settings = new ExportSettings { Width = 2560, Height = 1440, FramesPerSecond = 24, Crf = 22, CodecPreset = "fast", AudioBitrateKbps = 256 }, Custom = true }]);

        var preset = Assert.Single(store.Load());

        Assert.Equal("Мой рендер", preset.Name);
        Assert.Equal(2560, preset.Settings.Width);
        Assert.Equal(1440, preset.Settings.Height);
        Assert.Equal(24, preset.Settings.FramesPerSecond);
        Assert.Equal(22, preset.Settings.Crf);
        Assert.Equal("fast", preset.Settings.CodecPreset);
        Assert.Equal(256, preset.Settings.AudioBitrateKbps);
        Assert.True(preset.Custom);
    }

    [Fact]
    public void ПутьКFfmpeg_НеСохраняется()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        store.Save([new ExportPreset { Name = "Рендер", Settings = new ExportSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" } }]);

        Assert.Null(Assert.Single(store.Load()).Settings.FfmpegPath);
    }

    [Fact]
    public void ОтсутствующийФайл_ПустойСписок()
    {
        using var directory = new TemporaryDirectory();

        Assert.Empty(new ExportPresetStore(directory.File("нет-такого.json")).Load());
    }

    [Fact]
    public void НегодныйФайл_ПустойСписокИФайлОстаётся()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("presets.json");
        File.WriteAllText(path, "{ это не json");

        var store = new ExportPresetStore(path);

        Assert.Empty(store.Load());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ПовторноеСохранение_ЗаменяетСписок()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        store.Save([new ExportPreset { Name = "Первый" }]);
        store.Save([new ExportPreset { Name = "Второй" }]);

        Assert.Equal("Второй", Assert.Single(store.Load()).Name);
    }

    [Fact]
    public void НечётныеРазмеры_ПриводятсяКЧётным()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        store.Save([new ExportPreset { Name = "Кривой", Settings = new ExportSettings { Width = 1281, Height = 721 } }]);

        var settings = Assert.Single(store.Load()).Settings;

        Assert.Equal(1280, settings.Width);
        Assert.Equal(720, settings.Height);
    }

    [Fact]
    public void БезымянныйПресет_НеПопадаетВСписок()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        store.Save([new ExportPreset { Name = "  " }, new ExportPreset { Name = "  Нормальный  " }]);

        Assert.Equal("Нормальный", Assert.Single(store.Load()).Name);
    }

    [Fact]
    public void ОдинаковыеИмена_ОстаётсяОдин()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        store.Save([new ExportPreset { Name = "Рендер", Settings = new ExportSettings { Width = 640 } }]);
        store.Save([new ExportPreset { Name = "рендер", Settings = new ExportSettings { Width = 800 } }, new ExportPreset { Name = "Рендер", Settings = new ExportSettings { Width = 1024 } }]);

        Assert.Equal(800, Assert.Single(store.Load()).Settings.Width);
    }

    [Fact]
    public void ПутьККонструктору_Отклоняется()
        => Assert.Throws<ArgumentException>(() => new ExportPresetStore(" "));

    [Fact]
    public void ПустойСписок_Записывается()
    {
        using var directory = new TemporaryDirectory();
        var store = new ExportPresetStore(directory.File("presets.json"));

        Assert.True(store.Save([]));
        Assert.Empty(store.Load());
    }

    [Fact]
    public void НедоступныйКаталог_ЗаписьНеБросает()
    {
        // Путь ведёт в каталог, которого нет и который нельзя создать: ошибка записи
        // должна превращаться в false, а не ронять приложение по нажатию кнопки.
        var store = new ExportPresetStore(Path.Combine(Path.GetTempPath(), $"нет-каталога-{Guid.NewGuid():N}", "presets.json"));

        Assert.False(store.Save([new ExportPreset { Name = "Рендер" }]));
    }

    [Fact]
    public void РазборПустой_ПустойСписок()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("presets.json");
        File.WriteAllText(path, "null");

        Assert.Empty(new ExportPresetStore(path).Load());
    }
}