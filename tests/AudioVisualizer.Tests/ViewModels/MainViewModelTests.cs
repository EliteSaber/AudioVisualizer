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

    private static (MainViewModel ViewModel, TemporaryDirectory Directory) CreateViewModel(AppSettings? settings = null)
    {
        var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(directory.File("settings.json"));
        store.Save(settings ?? AppSettings.Default);

        return (new MainViewModel(store), directory);
    }

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

            viewModel.Settings.ApplySizePreset("720p30");

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