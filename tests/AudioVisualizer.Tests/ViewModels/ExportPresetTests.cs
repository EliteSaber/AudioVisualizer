using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.ViewModels;

/// <summary>
/// Пользовательские пресеты выгрузки и сброс настроек по вкладкам. Настоящий ffmpeg и
/// файлы настроек не нужны: проверяются команды панели и список пресетов.
/// </summary>
public class ExportPresetTests
{
    private static readonly string PresetsPath = Path.Combine(Path.GetTempPath(), "av-test-exportpresets-cmds.json");

    [Fact]
    public void НовыйПанель_СодержитВстроенныеПресеты()
    {
        var (viewModel, _) = Create();

        Assert.Equal(3, viewModel.ExportPresets.Count);
        Assert.Equal(["1080p60", "1080p30", "720p30"], viewModel.ExportPresets.Select(preset => preset.Name));
        Assert.All(viewModel.ExportPresets, preset => Assert.False(preset.Custom));
    }

    [Fact]
    public void КнопкаСохранитьКак_ПредлагаетТекущийРазмер()
    {
        var (viewModel, _) = Create();

        viewModel.StartNamingExportPresetCommand.Execute(null);

        Assert.True(viewModel.IsNamingExportPreset);
        Assert.Equal("1920x1080, 60", viewModel.NewExportPresetName);
    }

    [Fact]
    public void ПустоеИмя_НеДаётСохранить()
    {
        var (viewModel, _) = Create();

        viewModel.StartNamingExportPresetCommand.Execute(null);
        viewModel.NewExportPresetName = "   ";

        Assert.False(viewModel.CanSaveExportPreset);
        Assert.False(viewModel.SaveExportPresetCommand.CanExecute(null));
    }

    [Fact]
    public void Сохранение_ДобавляетПресетВСписокИФайл()
    {
        var (viewModel, store) = Create();

        viewModel.ExportWidth = 2560;
        viewModel.ExportHeight = 1440;
        viewModel.StartNamingExportPresetCommand.Execute(null);
        viewModel.NewExportPresetName = "Мой 1440p";
        viewModel.SaveExportPresetCommand.Execute(null);

        var saved = Assert.Single(viewModel.ExportPresets, preset => preset.Custom);
        Assert.Equal("Мой 1440p", saved.Name);
        Assert.Equal(2560, saved.Settings.Width);
        Assert.False(viewModel.IsNamingExportPreset);
        Assert.Same(saved, viewModel.SelectedExportPreset);

        // Пресет должен пережить перезапуск программы, поэтому он сразу попадает в файл.
        Assert.Contains(store.Load(), preset => preset.Name == "Мой 1440p");
    }

    [Fact]
    public void Сохранение_НеТащитПутьКFfmpegВФайл()
    {
        var (viewModel, store) = Create();
        viewModel.FfmpegPath = @"C:\tools\ffmpeg.exe";

        viewModel.StartNamingExportPresetCommand.Execute(null);
        viewModel.NewExportPresetName = "Без пути";
        viewModel.SaveExportPresetCommand.Execute(null);

        // Путь принадлежит машине: переносить его между компьютерами незачем.
        Assert.All(store.Load(), preset => Assert.Null(preset.Settings.FfmpegPath));
    }

    [Fact]
    public void ПовторноеИмя_ОбновляетПресетАНеДобавляетВторой()
    {
        var (viewModel, _) = Create();

        SavePreset(viewModel, "Мой рендер", width: 1280);
        SavePreset(viewModel, "Мой рендер", width: 2560);

        var presets = viewModel.ExportPresets.Where(preset => preset.Custom).ToArray();

        Assert.Single(presets);
        Assert.Equal(2560, presets[0].Settings.Width);
    }

    [Fact]
    public void ИмяВстроенногоПресетa_НеТрогаетЕго()
    {
        var (viewModel, _) = Create();

        SavePreset(viewModel, "1080p60", width: 800);

        // Подменив встроенный своим, пользователь потерял бы встроенный до перезапуска.
        var builtIn = Assert.Single(viewModel.ExportPresets, preset => preset.Name == "1080p60");

        Assert.False(builtIn.Custom);
        Assert.Equal(1920, builtIn.Settings.Width);
        Assert.Contains("занято", viewModel.PresetsMessage, StringComparison.Ordinal);
        Assert.True(viewModel.IsNamingExportPreset);
    }

    [Fact]
    public void СохранённыйПресет_ПоявляетсяПослеПерезапуска()
    {
        var (_, store) = Create();
        SavePreset(Create().Item1, "Ночной рендер", width: 800);

        var reloaded = new SettingsViewModel(AppSettings.Default, store);

        Assert.Contains(reloaded.ExportPresets, preset => preset.Name == "Ночной рендер" && preset.Custom);
    }

    /// <summary>
    /// Выбор после перезапуска. Имя пресета в настройках не хранится, панель сопоставляет
    /// параметры, поэтому отметка всегда истинна: показать пресет, не описывающий текущие
    /// настройки, отсюда невозможно.
    /// </summary>
    [Fact]
    public void ПослеПерезапуска_ОтмеченПресетСовпадающимиПараметрами()
    {
        var (viewModel, store) = Create();

        var preset = viewModel.ExportPresets.First(item => item.Name == "720p30");
        viewModel.SelectedExportPreset = preset;

        var reloaded = new SettingsViewModel(viewModel.ToAppSettings(), store);

        // Встроенные пресеты живут в статическом списке, поэтому это тот же самый пункт.
        Assert.Same(preset, reloaded.SelectedExportPreset);
    }

    [Fact]
    public void ПослеПерезапуска_ПравленыйПараметр_ОставляетСписокПустым()
    {
        // Совпадение по размеру ещё не совпадение: CRF в пресете один, а человек поменял.
        var (viewModel, store) = Create();

        viewModel.SelectedExportPreset = viewModel.ExportPresets.First(item => item.Name == "720p30");

        var settings = viewModel.ToAppSettings();
        settings = settings with { Export = settings.Export with { Crf = 30 } };

        var reloaded = new SettingsViewModel(settings, store);

        Assert.Null(reloaded.SelectedExportPreset);
    }

    [Fact]
    public void ПослеПерезапуска_УдалённыйПресетНеВозвращается()
    {
        // Пресет уехал в файл, настройки от него остались в настройках, а потом его удалили.
        // Настройки удалённого пресета остаются в силе - исчезать им незачем, - но отмечать
        // несуществующий пункт было бы враньём.
        var (_, store) = Create();
        var source = Create().Item1;

        SavePreset(source, "Ночной рендер", width: 800);
        var settings = source.ToAppSettings();

        source.DeleteExportPresetCommand.Execute(null);

        var reloaded = new SettingsViewModel(settings, store);

        Assert.Null(reloaded.SelectedExportPreset);
        Assert.Equal(800, reloaded.ExportWidth);
    }

    [Fact]
    public void СбросВыгрузки_НеВозвращаетОтмеченныйПресет()
    {
        // Восстановление живёт в конструкторе, а не в Apply: после сброса параметры стали
        // ровно как у «1080p60», и отметить его было бы можно только повторным применением
        // правила в Apply - то есть показать пресет, который человек только что отменил.
        var viewModel = Create().Item1;

        viewModel.SelectedExportPreset = viewModel.ExportPresets.First(item => item.Name == "720p30");
        Assert.Equal(1280, viewModel.ExportWidth);

        viewModel.ResetExportCommand.Execute(null);

        Assert.Equal(1920, viewModel.ExportWidth);
        Assert.Null(viewModel.SelectedExportPreset);
    }

    [Fact]
    public void Удаление_УбираетПресетИзСпискаИФайла()
    {
        var (viewModel, store) = Create();
        SavePreset(viewModel, "Лишний", width: 800);
        viewModel.SelectedExportPreset = viewModel.ExportPresets.Last(preset => preset.Custom);

        viewModel.DeleteExportPresetCommand.Execute(null);

        Assert.DoesNotContain(viewModel.ExportPresets, preset => preset.Name == "Лишний");
        Assert.Null(viewModel.SelectedExportPreset);
        Assert.DoesNotContain(store.Load(), preset => preset.Name == "Лишний");
    }

    [Fact]
    public void ВстроенныйПресетУдалитьНельзя()
    {
        var (viewModel, _) = Create();
        viewModel.SelectedExportPreset = viewModel.ExportPresets.First(preset => preset.Name == "1080p60");

        Assert.False(viewModel.CanDeleteExportPreset);
        Assert.False(viewModel.DeleteExportPresetCommand.CanExecute(null));
    }

    [Fact]
    public void СбросЦветов_МеняетТолькоЦвета()
    {
        var settings = AppSettings.Default with
        {
            Colors = new ColorSettings { Background = new RgbaColor(1, 2, 3) },
            Bars = new BarSettings { Width = 17 }
        };

        var viewModel = Create(settings).Item1;

        viewModel.ResetColorsCommand.Execute(null);

        Assert.Equal(new ColorSettings().Background, viewModel.BackgroundColor);
        Assert.Equal(17, viewModel.BarWidth);
    }

    [Fact]
    public void СбросСтолбиков_МеняетТолькоСтолбики()
    {
        var settings = AppSettings.Default with
        {
            Colors = new ColorSettings { Background = new RgbaColor(1, 2, 3) },
            Bars = new BarSettings { Width = 17 }
        };

        var viewModel = Create(settings).Item1;

        viewModel.ResetBarsCommand.Execute(null);

        Assert.Equal(new BarSettings().Width, viewModel.BarWidth);
        Assert.Equal(new RgbaColor(1, 2, 3), viewModel.BackgroundColor);
    }

    [Fact]
    public void СбросСпектра_ВозвращаетПлоскийЭквалайзер()
    {
        var settings = AppSettings.Default with
        {
            Spectrum = new SpectrumSettings { FftSize = 1024, Equalizer = [new EqualizerBand(1000f, 9f)] },
            Bars = new BarSettings { Width = 17 }
        };

        var viewModel = Create(settings).Item1;

        viewModel.ResetSpectrumCommand.Execute(null);

        Assert.Equal(SpectrumSettings.Default.FftSize, viewModel.FftSize);
        Assert.Equal(SpectrumSettings.DefaultEqualizer.Count, viewModel.Equalizer.Count);
        Assert.All(viewModel.Equalizer, band => Assert.Equal(0f, band.GainDb));

        // Остальные вкладки не должны затрагиваться.
        Assert.Equal(17, viewModel.BarWidth);
    }

    [Fact]
    public void СбросВыгрузки_СохраняетПутьКFfmpeg()
    {
        var settings = AppSettings.Default with
        {
            Export = new ExportSettings { Width = 640, Height = 480, Crf = 30, FfmpegPath = @"C:\tools\ffmpeg.exe" }
        };

        var viewModel = Create(settings).Item1;

        viewModel.ResetExportCommand.Execute(null);

        Assert.Equal(new ExportSettings().Width, viewModel.ExportWidth);
        Assert.Equal(new ExportSettings().Crf, viewModel.Crf);

        // Путь задаётся один раз на машину и не должен пропадать при сбросе параметров видео.
        Assert.Equal(@"C:\tools\ffmpeg.exe", viewModel.FfmpegPath);
    }

    [Fact]
    public void СбросВкладки_ПоднимаетChangedОдинРаз()
    {
        var viewModel = Create().Item1;
        var raised = 0;
        viewModel.Changed += (_, _) => raised++;

        viewModel.ResetBarsCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    private static void SavePreset(SettingsViewModel viewModel, string name, int width)
    {
        viewModel.ExportWidth = width;
        viewModel.StartNamingExportPresetCommand.Execute(null);
        viewModel.NewExportPresetName = name;
        viewModel.SaveExportPresetCommand.Execute(null);
    }

    private static (SettingsViewModel ViewModel, ExportPresetStore Store) Create(AppSettings? settings = null)
    {
        if (File.Exists(PresetsPath))
        {
            File.Delete(PresetsPath);
        }

        var store = new ExportPresetStore(PresetsPath);

        return (new SettingsViewModel(settings ?? AppSettings.Default, store), store);
    }
}