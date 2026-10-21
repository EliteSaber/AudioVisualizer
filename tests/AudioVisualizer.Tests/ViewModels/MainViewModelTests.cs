using System.Text.Json;
using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Audio;

namespace AudioVisualizer.Tests.ViewModels;

public class MainViewModelTests
{
    private static TemporaryAudioFile SineWav(int sampleRate = 44100, int length = 44100, float amplitude = 0.5f)
    {
        var samples = new float[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = amplitude * MathF.Sin(2f * MathF.PI * 1000f * i / sampleRate);
        }

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, sampleRate);

        return new TemporaryAudioFile(stream.ToArray());
    }

    private static (MainViewModel ViewModel, TemporaryDirectory Directory) CreateViewModel(
        AppSettings? settings = null,
        IAudioPlayer? player = null)
    {
        var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(directory.File("settings.json"));
        store.Save(settings ?? AppSettings.Default);

        var viewModel = player is null ? new MainViewModel(store) : new MainViewModel(store, player: player);

        return (viewModel, directory);
    }

    [Fact]
    public void НеудачноеСохранение_ПишетВСтатус()
    {
        using var directory = new TemporaryDirectory();

        // Запись идёт в несуществующий каталог: раньше причина молча терялась, и о том, что
        // настройки не записаны, можно было узнать только при следующем запуске.
        var viewModel = new MainViewModel(new AppSettingsStore(Path.Combine(directory.Root, "нет", "settings.json")));

        viewModel.SaveSettingsCommand.Execute(null);

        Assert.Contains("Не удалось сохранить настройки", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void УспешноеСохранение_СтатусНеМеняет()
    {
        var (viewModel, directory) = CreateViewModel();

        // Об успехе сообщать нечем: это обычный порядок вещей, и надпись после каждого выхода
        // только занимала бы строку статуса.
        viewModel.SaveSettingsCommand.Execute(null);

        Assert.DoesNotContain("сохранить", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ПравкаНастройки_СтавитЗаписьВОчередьНоНеПишетФайл()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            // Главная проверка отложенной записи: правка не должна писать файл сразу, иначе
            // жест ползунка переписывал бы его десятки раз в секунду.
            viewModel.Settings.BarWidth = 17;

            Assert.True(viewModel.SavePending);
            Assert.Equal(AppSettings.Default.Bars.Width, ReadWidth(directory.File("settings.json")));
        }
    }

    [Fact]
    public void FlushSave_ПишетФайлИСнимаетОчередь()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Settings.BarWidth = 17;
            viewModel.FlushSave();

            Assert.False(viewModel.SavePending);
            Assert.Equal(17, ReadWidth(directory.File("settings.json")));

            // Второй выпуск ничего не пишет: очередь уже снята.
            viewModel.Settings.BarWidth = 19;
            viewModel.Settings.BarWidth = 17;
            viewModel.FlushSave();

            Assert.Equal(17, ReadWidth(directory.File("settings.json")));
        }
    }

    [Fact]
    public void СбросНастроек_ТожеСтавитЗаписьВОчередь()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            // Сброс идёт через Apply, который поднимает Changed один раз в конце. Если это
            // перестанет происходить, сброс перестанет сохраняться до выхода из окна - и
            // будет выглядеть как «иногда сбрасывается, иногда нет».
            viewModel.Settings.BarWidth = 17;
            viewModel.ResetSettingsCommand.Execute(null);

            Assert.True(viewModel.SavePending);
        }
    }

    [Fact]
    public void Громкость_БерётсяИзФайла_ИИдётВПлеер()
    {
        var player = new FakeAudioPlayer();
        var (viewModel, directory) = CreateViewModel(AppSettings.Default with { Volume = 0.4d }, player);
        using (directory)
        {
            Assert.Equal(0.4d, viewModel.Volume);

            // Плееру громкость нужна ещё до первого Play: открытый трек обязан звучать
            // на той громкости, что стоит на ползунке, а не на умолчании.
            Assert.Contains(0.4d, player.SetVolumes);
        }
    }

    [Fact]
    public void ПравкаГромкости_ИдётВПлеерИСтавитЗаписьВОчередь()
    {
        var player = new FakeAudioPlayer();
        var (viewModel, directory) = CreateViewModel(player: player);
        using (directory)
        {
            viewModel.Volume = 0.25d;

            Assert.Equal(0.25d, player.SetVolumes[^1]);
            Assert.True(viewModel.SavePending);
        }
    }

    [Fact]
    public void Громкость_СохраняетсяВФайл()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 0.25d;
            viewModel.FlushSave();

            Assert.Equal(0.25d, new AppSettingsStore(directory.File("settings.json")).Load().Volume);
        }
    }

    [Fact]
    public void ВыключениеЗвука_СбрасываетВНольИВозвращаетПрежнююГромкость()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 0.6d;

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal(0d, viewModel.Volume);
            Assert.True(viewModel.IsMuted);

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal(0.6d, viewModel.Volume);
            Assert.False(viewModel.IsMuted);
        }
    }

    [Fact]
    public void ЗапускСНулевойГромкостью_ВозвратЗвука_Полная()
    {
        var (viewModel, directory) = CreateViewModel(AppSettings.Default with { Volume = 0d });
        using (directory)
        {
            Assert.True(viewModel.IsMuted);

            // Прежняя громкость неизвестна: в файле ноль, и восстанавливать нечего - тогда
            // возвращается полная.
            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal(1d, viewModel.Volume);
            Assert.False(viewModel.IsMuted);
        }
    }

    [Fact]
    public void ГромкостьУНуляЧерезПолзунок_КнопкаВозвращаетПрежнюю()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 0.3d;

            // Взятие ползунка запоминает уровень: к моменту, когда громкость станет нулём,
            // промежуточных значений будет много, а вернуть надо тот, что был у человека.
            viewModel.RememberVolume();
            viewModel.Volume = 0d;

            Assert.True(viewModel.IsMuted);

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal(0.3d, viewModel.Volume);
        }
    }

    [Fact]
    public void ПромежуточныеЗначенияПолзунка_НеМешаютВозвратуГромкости()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 0.3d;
            viewModel.RememberVolume();

            // Перетаскивание в ноль идёт десятками значений, и последнее ненулевое из них -
            // это процент. Вернуть его - значит включить почти тишину.
            foreach (var value in new[] { 0.29d, 0.15d, 0.07d, 0.01d, 0d })
            {
                viewModel.Volume = value;
            }

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal(0.3d, viewModel.Volume);
        }
    }

    [Fact]
    public void ВыключениеЗвука_ПослеУменьшения_ВозвращаетТочнуюГромкость()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 1d;
            viewModel.Volume = 0.6d;

            // Кнопка знает громкость точно, и её значение важнее следа от ползунка: тот помнит
            // уровень, с которого громкость последний раз поднимали.
            viewModel.ToggleMuteCommand.Execute(null);
            Assert.Equal(0d, viewModel.Volume);

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal(0.6d, viewModel.Volume);
        }
    }

    [Fact]
    public void СбросНастроек_ВозвращаетПолнуюГромкость()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 0d;

            // Громкость живёт в модели, а не в панели, поэтому сброс панели её не трогал:
            // после сброса звук оставался выключенным, хотя кнопка обещает «сбросить все
            // настройки».
            viewModel.ResetSettingsCommand.Execute(null);

            Assert.Equal(1d, viewModel.Volume);
        }
    }

    [Fact]
    public void Громкость_ВыходитВПодсказкуПроцентами()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            Assert.Equal("Громкость: 100%", viewModel.VolumeLabel);

            viewModel.Volume = 0.25d;
            Assert.Equal("Громкость: 25%", viewModel.VolumeLabel);

            // Края подсказки обязаны быть читаемыми: ноль есть тишина, а не «0% звук».
            viewModel.Volume = 0d;
            Assert.Equal("Громкость: 0%", viewModel.VolumeLabel);

            viewModel.Volume = 1d;
            Assert.Equal("Громкость: 100%", viewModel.VolumeLabel);
        }
    }

    [Fact]
    public void ВыключениеЗвука_ОбновляетПодсказку()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Volume = 0.6d;

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal("Громкость: 0%", viewModel.VolumeLabel);

            viewModel.ToggleMuteCommand.Execute(null);

            Assert.Equal("Громкость: 60%", viewModel.VolumeLabel);
        }
    }

    [Fact]
    public async Task ПовторныйОтказСохранить_СтрокуНеПеретирает()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new AppSettingsStore(Path.Combine(directory.Root, "нет", "settings.json")));

        viewModel.SaveSettingsCommand.Execute(null);
        Assert.True(viewModel.IsStatusError);

        using var sine = SineWav();
        await viewModel.LoadFileAsync(sine.Path);
        var status = viewModel.Status;
        Assert.False(viewModel.IsStatusError);

        // Второй отказ молчит: текст появляется один раз на сбой, а не на каждую правку,
        // иначе он бы затирал сведения о треке.
        viewModel.SaveSettingsCommand.Execute(null);

        Assert.Equal(status, viewModel.Status);
    }

    [Fact]
    public void УспешноеСохранение_СнимаетОтметкуОбОтказе()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.SaveSettingsCommand.Execute(null);

            Assert.False(viewModel.IsStatusError);
        }
    }

    [Fact]
    public async Task ОшибкаОткрытияФайла_КраситСтрокуАОбычноеСообщениеГасит()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            // Неудача - красная строка, а обычное сообщение про трек её гасит: иначе красный
            // цвет остался бы от прежней ошибки навсегда.
            await viewModel.LoadFileAsync(@"Z:\нет\такого.mp3");
            Assert.True(viewModel.IsStatusError);

using var sine = SineWav();
            await viewModel.LoadFileAsync(sine.Path);
            Assert.False(viewModel.IsStatusError);
        }
    }

    private static int ReadWidth(string path)
        => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)).GetProperty("Bars").GetProperty("Width").GetInt32();

    [Fact]
    public void Создание_БерётНастройкиИзФайла()
    {
        var (viewModel, directory) = CreateViewModel(AppSettings.Default with { Bars = new BarSettings { Width = 21 } });

        using (directory)
        {
            Assert.Equal(21, viewModel.Settings.BarWidth);
            Assert.Equal(1920, viewModel.PreviewWidth);
            Assert.Equal(1080, viewModel.PreviewHeight);
        }
    }

    [Fact]
    public async Task ЗагрузкаФайла_ЗаполняетКадрИСведения()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        using (var file = SineWav())
        {
            await viewModel.LoadFileAsync(file.Path);

            Assert.Equal(Path.GetFileName(file.Path), viewModel.FileTitle);
            Assert.Equal(TimeSpan.FromSeconds(1d), viewModel.Duration);
            Assert.NotEmpty(viewModel.Snapshot.Values);
            Assert.Contains("44100", viewModel.Status);
        }
    }

    [Fact]
    public async Task ЗагрузкаФайла_ЧислоЗначений_РавноЧислуСтолбиков()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        using (var file = SineWav(length: 8000))
        {
            await viewModel.LoadFileAsync(file.Path);

            var layout = new Core.Rendering.BarLayout(viewModel.PreviewWidth, viewModel.PreviewHeight, viewModel.Settings.ToAppSettings().Bars);

            Assert.Equal(layout.BarCount, viewModel.Snapshot.Values.Length);
        }
    }

    [Fact]
    public async Task ЗагрузкаНесуществующегоФайла_ПишетВСтатус()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            await viewModel.LoadFileAsync(@"Z:\нет\такого.mp3");

            Assert.Contains("Не удалось открыть файл", viewModel.Status);
            Assert.All(viewModel.Snapshot.Values, value => Assert.Equal(0f, value));
        }
    }

    [Fact]
    public async Task ЗагрузкаБитогоФайла_ПишетВСтатус()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        using (var file = new TemporaryAudioFile([1, 2, 3, 4], extension: ".bin"))
        {
            await viewModel.LoadFileAsync(file.Path);

            Assert.Contains("Не удалось открыть файл", viewModel.Status);
        }
    }

    [Fact]
    public async Task ОшибкаОткрытияФайла_ПоднимаетОтчётДляОкна()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            var reports = new List<Notice>();
            viewModel.NoticeRequested = reports.Add;

            await viewModel.LoadFileAsync(@"Z:\нет\такого.mp3");

            // Ошибка открытия не про ffmpeg, поэтому папки нет и кнопка «открыть папку»
            // у окна показываться не должна.
            var notice = Assert.Single(reports);
            Assert.Equal("Не удалось открыть файл", notice.Title);
            Assert.Contains("такого.mp3", notice.Message, StringComparison.Ordinal);
            Assert.Null(notice.FolderPath);
        }
    }

[Fact]
    public async Task ОшибкаОткрытия_ВСтрокеЗаголовокАПричинаВПодсказке()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            await viewModel.LoadFileAsync(@"Z:\нет\такого.mp3");

            // Причина открытия видна не сразу: окно показывает её с прокруткой, а строка
            // статуса - только заголовок, который и без обрезки помещается целиком.
            Assert.Equal("Не удалось открыть файл", viewModel.Status);
            Assert.Contains("такого.mp3", viewModel.StatusDetail, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ИзменениеНастроек_ПересчитываетКадр()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        using (var file = SineWav())
        {
            await viewModel.LoadFileAsync(file.Path);
            var before = viewModel.Snapshot;

            viewModel.Settings.BarWidth = 40;

            Assert.NotSame(before, viewModel.Snapshot);
            Assert.Equal(40, viewModel.Snapshot.Bars.Width);
            Assert.Equal(AppSettings.Default.Bars.Spacing, viewModel.Snapshot.Bars.Spacing);
        }
    }

    [Fact]
    public async Task ИзменениеНастроекЭкспорта_МеняетРазмерПревью()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            await viewModel.LoadFileAsync("Z:\\нет.mp3");

            viewModel.Settings.ApplyExportPreset(
                new ExportPreset { Name = "720p30", Settings = new ExportSettings { Width = 1280, Height = 720, FramesPerSecond = 30 } });

            Assert.Equal(1280, viewModel.PreviewWidth);
            Assert.Equal(720, viewModel.PreviewHeight);
        }
    }

    [Fact]
    public void SaveSettings_ПишетФайл()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Settings.BarWidth = 33;

            viewModel.SaveSettingsCommand.Execute(null);

            Assert.Equal(33, new AppSettingsStore(directory.File("settings.json")).Load().Bars.Width);
        }
    }

    [Fact]
    public void ResetSettings_ВозвращаетУмолчания()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            viewModel.Settings.BarWidth = 33;

            viewModel.ResetSettingsCommand.Execute(null);

            Assert.Equal(AppSettings.Default.Bars.Width, viewModel.Settings.BarWidth);
        }
    }

    [Fact]
    public async Task ПустойПуть_Отклоняется()
    {
        var (viewModel, directory) = CreateViewModel();
        using (directory)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => viewModel.LoadFileAsync("  "));
        }
    }
}