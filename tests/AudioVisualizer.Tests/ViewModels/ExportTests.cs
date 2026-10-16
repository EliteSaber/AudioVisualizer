using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Audio;

namespace AudioVisualizer.Tests.ViewModels;

/// <summary>
/// Выгрузка видео из окна. Настоящий ffmpeg в тестах не нужен: проверяется, что окно
/// собрало запрос из настроек, показало прогресс и пережило ошибку.
/// </summary>
public class ExportTests
{
    private static readonly TemporaryAudioFile SharedSine = new(CreateSineWav(), extension: ".wav");

    [Fact]
    public async Task БезФайла_ВыгрузкаНеНачинается()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            Assert.False(viewModel.CanExport);

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            Assert.Equal(0, exporter.Calls);
            Assert.Contains("аудиофайл", viewModel.Status, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Выгрузка_ПередаётНастройкиИПуть()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            viewModel.Settings.ExportWidth = 640;
            viewModel.Settings.ExportHeight = 360;

            var output = Path.Combine(directory.Root, "итог.mp4");
            await viewModel.ExportVideoAsync(output);

            var request = Assert.Single(exporter.Requests);
            Assert.Equal(output, request.OutputPath);
            Assert.Equal(640, request.Settings.Export.Width);
            Assert.Equal(360, request.Settings.Export.Height);

            // Аудио для mp4 берётся из уже декодированного трека, а не читается заново:
            // второй проход декодирования дал бы другой звук в исходном формате.
            Assert.Equal(44100, request.Audio.SampleRate);
            Assert.Equal(44100, request.Audio.Length);
        }
    }

    [Fact]
    public async Task Выгрузка_ПоказываетПрогрессИГотовность()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            exporter.OnExport = (progress, _) =>
            {
                progress.Report(new VideoExportProgress(30, 60, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

                return Task.FromResult(new VideoExportResult("итог.mp4", 60, TimeSpan.FromSeconds(10)));
            };

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            Assert.Equal(1d, viewModel.ExportProgress);
            Assert.Contains("Готово", viewModel.Status, StringComparison.Ordinal);
            Assert.Contains("60", viewModel.Status, StringComparison.Ordinal);

            // После выгрузки окно снова пригодно для работы.
            Assert.False(viewModel.IsExporting);
            Assert.True(viewModel.CanExport);
            Assert.Equal(string.Empty, viewModel.ExportProgressText);
        }
    }

    [Fact]
    public async Task ОшибкаВыгрузки_ВСтрокуИдётТолькоЗаголовок()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            exporter.Failure = new VideoExportException("ffmpeg не найден");

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            // Строка обрезала бы подробности многоточием, поэтому в ней остаётся заголовок,
            // а весь текст живёт в подсказке и в окне.
            Assert.Equal("Не удалось выгрузить видео", viewModel.Status);
            Assert.Equal("ffmpeg не найден", viewModel.StatusDetail);
            Assert.True(viewModel.IsStatusError);
            Assert.False(viewModel.IsExporting);
            Assert.True(viewModel.CanExport);
        }
    }

    [Fact]
    public async Task ОбычноеСообщение_ГаситПодробностиПрежнейОшибки()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            exporter.Failure = new VideoExportException("ffmpeg перестал читать кадры");

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));
            Assert.NotEmpty(viewModel.StatusDetail);

            // Иначе подсказка на строке показывала бы ошибку, которой на экране уже нет.
            await viewModel.LoadFileAsync(SharedSine.Path);

            Assert.Equal(string.Empty, viewModel.StatusDetail);
            Assert.False(viewModel.IsStatusError);
        }
    }

    [Fact]
    public async Task ОшибкаВыгрузки_ПоднимаетУведомлениеДляОкна()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            var notices = new List<Notice>();
            viewModel.NoticeRequested = notices.Add;

            exporter.Failure = new VideoExportException("ffmpeg перестал читать кадры");

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            // Заголовок по смыслу и текст с подробностями. Папки у ошибки нет: ролик не
            // появился, а журнала на диске больше не остаётся.
            var notice = Assert.Single(notices);
            Assert.Equal("Не удалось выгрузить видео", notice.Title);
            Assert.Equal("ffmpeg перестал читать кадры", notice.Message);
            Assert.Null(notice.FolderPath);
        }
    }

    [Fact]
    public async Task УспешнаяВыгрузка_ПоднимаетУведомление()
    {
        var (viewModel, _, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            var notices = new List<Notice>();
            viewModel.NoticeRequested = notices.Add;

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            var notice = Assert.Single(notices);
            Assert.Equal("Выгрузка готова", notice.Title);
            Assert.Contains("кадров", notice.Message, StringComparison.Ordinal);

            // Полный путь нужен в окне: в строке статуса остаётся имя, а пользователю
            // после выгрузки нужен именно он.
            Assert.Contains(Path.Combine(directory.Root, "итог.mp4"), notice.Message, StringComparison.Ordinal);
            Assert.Equal(directory.Root, notice.FolderPath);
        }
    }

    [Fact]
    public async Task ТихаяВыгрузка_ОбУспехеОкнаНеПоказывает()
    {
        var (viewModel, _, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            var notices = new List<Notice>();
            viewModel.NoticeRequested = notices.Add;
            viewModel.Settings.QuietExport = true;

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            // Галочка про успех: строка статуса на месте, а лишнего окна нет.
            Assert.Empty(notices);
            Assert.Contains("Готово", viewModel.Status, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ТихаяВыгрузка_ОбОшибкеОкноПоказывает()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            var notices = new List<Notice>();
            viewModel.NoticeRequested = notices.Add;
            viewModel.Settings.QuietExport = true;

            exporter.Failure = new VideoExportException("ffmpeg перестал читать кадры");

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            // Ошибку пропускать нельзя: галочка обещает только обратное.
            Assert.Equal("Не удалось выгрузить видео", Assert.Single(notices).Title);
        }
    }

    [Fact]
    public async Task Отмена_ОстанавливаетВыгрузку()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            exporter.OnExport = async (_, _) =>
            {
                viewModel.CancelExportCommand.Execute(null);

                // Настоящий экспортёр после отмены возвращает признак отмены, а не бросает.
                await Task.Delay(50, CancellationToken.None);

                return new VideoExportResult("итог.mp4", 0, TimeSpan.Zero, Cancelled: true);
            };

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            Assert.Contains("отменена", viewModel.Status, StringComparison.OrdinalIgnoreCase);
            Assert.False(viewModel.IsExporting);

            // Прогресс не должен доезжать до конца: выгрузки не было.
            Assert.True(viewModel.ExportProgress < 1d);
        }
    }

    [Fact]
    public async Task Выгрузка_ОстанавливаетВоспроизведение()
    {
        var (viewModel, exporter, directory, player) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            Assert.True(viewModel.IsPlaying);

            await viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));

            Assert.Equal(1, player.PauseCount);
            Assert.False(viewModel.IsPlaying);
            Assert.Equal(1, exporter.Calls);
        }
    }

    [Fact]
    public async Task ЗакрытиеОкна_ОтменяетИдущуюВыгрузку()
    {
        var (viewModel, exporter, directory, _) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            exporter.OnExport = async (_, token) =>
            {
                started.SetResult(token);

                // Выгрузка висит до отмены: иначе она успела бы завершиться раньше, чем
                // тест закроет окно, и проверять было бы нечего.
                await Task.Delay(Timeout.Infinite, token);

                return new VideoExportResult("итог.mp4", 0, TimeSpan.Zero);
            };

            var export = viewModel.ExportVideoAsync(Path.Combine(directory.Root, "итог.mp4"));
            var token = await started.Task;

            viewModel.Dispose();

            // Окно закрывается - вместе с ним должен остановиться и ffmpeg, и поток рендера.
            Assert.True(token.IsCancellationRequested);
            await export;
        }
    }

    [Fact]
    public async Task ПустойПутьВыгрузки_Отклоняется()
    {
        var (viewModel, _, directory, _) = Create();
        using (directory)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => viewModel.ExportVideoAsync(" "));
        }
    }

    private static (MainViewModel ViewModel, FakeVideoExporter Exporter, TemporaryDirectory Directory, FakeAudioPlayer Player) Create()
    {
        var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(directory.File("settings.json"));
        var exporter = new FakeVideoExporter();
        var player = new FakeAudioPlayer();

        var viewModel = new MainViewModel(store, new AudioLoader(), player, exporter);

        return (viewModel, exporter, directory, player);
    }

    private static byte[] CreateSineWav()
    {
        var samples = new float[44100];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / 44100);
        }

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, 44100);

        return stream.ToArray();
    }

    /// <summary>Экспортёр без ffmpeg: считает вызовы и умеет отменяться или падать.</summary>
    private sealed class FakeVideoExporter : IVideoExporter
    {
        public List<VideoExportRequest> Requests { get; } = [];

        public int Calls => Requests.Count;

        public Exception? Failure { get; set; }

        public Func<IProgress<VideoExportProgress>, CancellationToken, Task<VideoExportResult>>? OnExport { get; set; }

        public Task<VideoExportResult> ExportAsync(
            VideoExportRequest request,
            IProgress<VideoExportProgress>? progress,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (Failure is not null)
            {
                throw Failure;
            }

            if (OnExport is not null)
            {
                return OnExport(progress!, cancellationToken);
            }

            return Task.FromResult(new VideoExportResult(request.OutputPath, 60, TimeSpan.FromSeconds(10)));
        }
    }
}