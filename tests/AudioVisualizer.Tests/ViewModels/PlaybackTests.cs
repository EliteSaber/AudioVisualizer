using AudioVisualizer.App.ViewModels;
using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;
using AudioVisualizer.Tests.Audio;

namespace AudioVisualizer.Tests.ViewModels;

public class PlaybackTests
{
    /// <summary>
    /// Высота столбика тона: полоса 1 кГц при амплитуде 0,5 упирается в верх диапазона,
    /// а тишина даёт ровно ноль. Сравнивать точные значения нельзя: правка настроек
    /// пересоздаёт конвейер, а первый кадр после неё сглаживатель отдаёт как есть.
    /// </summary>
    private const float LoudBar = 0.5f;

    /// <summary>
    /// Порог видимого столбика на месте тонка, который уже закончился: сглаживание держит
    /// хвост ещё пару кадров, и он должен остаться после правки настроек.
    /// </summary>
    private const float TailBar = 0.25f;

    private static readonly TemporaryAudioFile SharedSine = new(CreateSineWav(), extension: ".wav");

    /// <summary>
    /// Половина секунды тишины, потом тон. Ровный синус годится не везде: в нём спектр
    /// одинаков в любой момент, и «кадр нарисован не с того места» на нём не увидеть.
    /// </summary>
    private static readonly TemporaryAudioFile SharedLateTone = new(CreateLateToneWav(), extension: ".wav");

    /// <summary>Половина секунды тона, потом тишина: на хвосте виден вклад сглаживания.</summary>
    private static readonly TemporaryAudioFile SharedEarlyTone = new(CreateEarlyToneWav(), extension: ".wav");

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

    private static byte[] CreateLateToneWav()
    {
        var samples = new float[44100];
        for (var i = 22050; i < samples.Length; i++)
        {
            samples[i] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / 44100);
        }

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, 44100);

        return stream.ToArray();
    }

    private static byte[] CreateEarlyToneWav()
    {
        var samples = new float[44100];
        for (var i = 0; i < 22050; i++)
        {
            samples[i] = 0.5f * MathF.Sin(2f * MathF.PI * 1000f * i / 44100);
        }

        using var stream = new MemoryStream();
        WavCodec.WritePcm16(stream, samples, 44100);

        return stream.ToArray();
    }

    private static (MainViewModel ViewModel, FakeAudioPlayer Player, TemporaryDirectory Directory) Create()
    {
        var directory = new TemporaryDirectory();
        var store = new AppSettingsStore(directory.File("settings.json"));
        var player = new FakeAudioPlayer();

        return (new MainViewModel(store, new AudioLoader(), player), player, directory);
    }

    [Fact]
    public async Task ЗагрузкаФайла_ВключаетКомандыВоспроизведения()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            Assert.False(viewModel.HasTrack);

            await viewModel.LoadFileAsync(SharedSine.Path);

            Assert.True(viewModel.HasTrack);
            Assert.Equal(TimeSpan.FromSeconds(1d), viewModel.Duration);
        }
    }

    [Fact]
    public async Task Воспроизведение_ПередаётПутьВПлеер()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            viewModel.TogglePlayCommand.Execute(null);

            Assert.Equal(1, player.PlayCount);
            Assert.Equal(SharedSine.Path, player.PlayedPath);
            Assert.True(viewModel.IsPlaying);
        }
    }

    [Fact]
    public async Task ПовторноеНажатие_СтавитНаПаузу()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            viewModel.TogglePlayCommand.Execute(null);
            viewModel.TogglePlayCommand.Execute(null);

            Assert.Equal(1, player.PauseCount);
            Assert.False(viewModel.IsPlaying);
        }
    }

    [Fact]
    public async Task Стоп_ВозвращаетПозициюВНачало()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            player.AdvanceTo(TimeSpan.FromSeconds(5d));

            viewModel.StopPlaybackCommand.Execute(null);

            Assert.True(player.StopCount > 0);
            Assert.Equal(0d, viewModel.PositionSeconds);
            Assert.False(viewModel.IsPlaying);
        }
    }

    [Fact]
    public async Task Воспроизведение_БезФайла_НичегоНеДелает()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            viewModel.TogglePlayCommand.Execute(null);

            Assert.Equal(0, player.PlayCount);
            Assert.False(viewModel.IsPlaying);
        }
    }

    [Fact]
    public async Task ОшибкаПлеера_ПишетсяВСтатус_ИНеВключаетВоспроизведение()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            player.PlayFailure = new InvalidOperationException("формат не играется");

            viewModel.TogglePlayCommand.Execute(null);

            Assert.False(viewModel.IsPlaying);
            Assert.Contains("Не удалось включить воспроизведение", viewModel.Status);

            // В строке только заголовок, а сама причина - в подсказке строки.
            Assert.Equal("формат не играется", viewModel.StatusDetail);
            Assert.DoesNotContain("формат не играется", viewModel.Status, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UpdatePlayback_БерётПозициюУПлеера()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            player.AdvanceTo(TimeSpan.FromSeconds(0.5d));

            viewModel.UpdatePlayback();

            Assert.Equal(0.5d, viewModel.PositionSeconds, 5);
        }
    }

    [Fact]
    public async Task UpdatePlayback_ВКонцеТрека_ОстанавливаетВоспроизведение()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            player.AdvanceTo(TimeSpan.FromSeconds(1.5d));

            viewModel.UpdatePlayback();

            Assert.False(viewModel.IsPlaying);
            Assert.Equal(0d, viewModel.PositionSeconds);
        }
    }

    [Fact]
    public async Task НачатьВоспроизведение_СнимаетЗависшуюПеремотку()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            // Отпускание мыши пропало за пределами окна: флаг перемотки остался поднятым.
            viewModel.BeginScrub();
            player.AdvanceTo(TimeSpan.FromSeconds(0.2d));
            viewModel.UpdatePlayback();
            Assert.Equal(0d, viewModel.PositionSeconds);

            viewModel.TogglePlayCommand.Execute(null);
            player.AdvanceTo(TimeSpan.FromSeconds(0.4d));
            viewModel.UpdatePlayback();

            Assert.Equal(0.4d, viewModel.PositionSeconds, 5);
        }
    }

    [Fact]
    public async Task ПеремоткаВТужеТочку_НеВызываетSeek()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);

            // Щелчок по самому бегунку: позиция не изменилась, звук перезапускать незачем.
            viewModel.EndScrub(viewModel.PositionSeconds);

            Assert.Empty(player.Seeks);
        }
    }

    [Fact]
    public async Task ПеремоткаВДругуюТочку_ВызываетSeek()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);

            viewModel.EndScrub(0.5d);

            Assert.Single(player.Seeks);
            Assert.Equal(TimeSpan.FromSeconds(0.5d), player.Seeks[0]);
        }
    }

    [Fact]
    public async Task UpdatePlayback_ПубликуетПозициюВПодписку()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            var published = new List<double>();
            viewModel.PositionChanged = published.Add;
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            player.AdvanceTo(TimeSpan.FromSeconds(0.3d));

            viewModel.UpdatePlayback();

            Assert.Equal(0.3d, published[^1], 5);
        }
    }

    [Fact]
    public async Task ПокаИдётПеремотка_ПозицияВПодпискуНеИдёт()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            var published = new List<double>();
            viewModel.PositionChanged = published.Add;
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            published.Clear();

            viewModel.BeginScrub();
            player.AdvanceTo(TimeSpan.FromSeconds(0.2d));
            viewModel.UpdatePlayback();

            // Значение, выбранное пользователем, не должно затираться прежней позицией.
            Assert.Empty(published);
        }
    }

    [Fact]
    public async Task Перемотка_ПубликуетЗапрошеннуюПозицию()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            var published = new List<double>();
            viewModel.PositionChanged = published.Add;
            await viewModel.LoadFileAsync(SharedSine.Path);

            viewModel.EndScrub(0.5d);

            Assert.Equal(0.5d, published[^1], 5);
        }
    }

    [Fact]
    public async Task ПеремоткаДоЗапуска_НеПадает_ИДвигаетКадр()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            var before = viewModel.Snapshot;

            // Сценарий пользователя: файл открыт, но ещё не играл, ползунок потянули.
            viewModel.EndScrub(0.5d);

            Assert.Equal(0.5d, viewModel.PositionSeconds, 5);
            Assert.Equal(TimeSpan.FromSeconds(0.5d), player.Position);
            Assert.NotSame(before, viewModel.Snapshot);
        }
    }

    [Fact]
    public async Task ПеремоткаДоЗапуска_ПотомВоспроизведение_НеПадает()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.EndScrub(0.5d);

            viewModel.TogglePlayCommand.Execute(null);

            Assert.Equal(1, player.PlayCount);
            Assert.True(viewModel.IsPlaying);
            Assert.Equal(0.5d, viewModel.PositionSeconds, 5);
        }
    }

    [Fact]
    public async Task Перемотка_ОграничиваетПозициюДлительностью()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            viewModel.EndScrub(999d);

            Assert.Equal(TimeSpan.FromSeconds(1d), player.Seeks[0]);
            Assert.Equal(1d, viewModel.PositionSeconds, 5);
        }
    }

    [Fact]
    public async Task ПеремоткаОтрицательная_ИдётВНачало()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            viewModel.EndScrub(0.5d);

            viewModel.EndScrub(-10d);

            Assert.Equal(TimeSpan.Zero, player.Seeks[^1]);
            Assert.Equal(0d, viewModel.PositionSeconds);
        }
    }

    [Fact]
    public async Task Перемотка_ПокаИдёт_НеДаётТаймеруВернутьПозицию()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            player.AdvanceTo(TimeSpan.FromSeconds(0.2d));

            viewModel.BeginScrub();
            viewModel.UpdatePlayback();

            Assert.Equal(0d, viewModel.PositionSeconds);
        }
    }

    [Fact]
    public async Task Время_ПоказываетсяВВиде_МинутыСекунды()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);

            Assert.Equal("00:00 / 00:01", viewModel.TimeLabel);
        }
    }

    [Fact]
    public async Task НовыйФайл_ОстанавливаетПрежний()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.TogglePlayCommand.Execute(null);
            var stopsAfterPlay = player.StopCount;

            await viewModel.LoadFileAsync(SharedSine.Path);

            Assert.True(player.StopCount > stopsAfterPlay);
            Assert.False(viewModel.IsPlaying);
            Assert.Equal(0d, viewModel.PositionSeconds);
        }
    }

    [Fact]
    public async Task НастройкиЭкспорта_НеЛомаютЧастотуОбновления()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedSine.Path);
            viewModel.Settings.Apply(AppSettings.Default with { Export = new ExportSettings { FramesPerSecond = 240 } });

            viewModel.TogglePlayCommand.Execute(null);

            Assert.True(viewModel.IsPlaying);
        }
    }

    [Fact]
    public async Task ИзменениеНастроек_КадрОстаётсяНаТекущейПозиции()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedLateTone.Path);

            // Предусловие: в начале файла тишина, поэтому «кадр уехал в начало» видно сразу.
            Assert.DoesNotContain(viewModel.Snapshot.Values, value => value > LoudBar);

            viewModel.EndScrub(0.75d);
            viewModel.Settings.BarWidth = 40;

            Assert.Contains(viewModel.Snapshot.Values, value => value > LoudBar);
        }
    }

    [Fact]
    public async Task СтопПотомИзменениеНастроек_КадрОстаётсяВНачале()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedLateTone.Path);
            viewModel.EndScrub(0.75d);

            viewModel.StopPlaybackCommand.Execute(null);
            viewModel.Settings.BarWidth = 40;

            // Момент для кадра берётся у плеера, а не запоминается: после стопа он в начале.
            Assert.DoesNotContain(viewModel.Snapshot.Values, value => value > LoudBar);
        }
    }

[Fact]
    public async Task ПравкаЦвета_НеСдвигаетСтолбики()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedEarlyTone.Path);
            viewModel.EndScrub(0.6d);
            var onScreen = viewModel.Snapshot.Values;

            // Правки идут цепочкой: каждая постройка источника берёт кадр у предыдущей,
            // и уже со второй спектр пересчитывался заново.
            viewModel.Settings.BarFillColor = new RgbaColor(10, 200, 90);
            Assert.Equal(onScreen, viewModel.Snapshot.Values);

            viewModel.Settings.BackgroundColor = new RgbaColor(1, 2, 3);
            Assert.Equal(onScreen, viewModel.Snapshot.Values);

            viewModel.Settings.CenterLineColor = new RgbaColor(4, 5, 6, 200);
            Assert.Equal(onScreen, viewModel.Snapshot.Values);

            viewModel.Settings.BarSecondaryColor = new RgbaColor(200, 10, 90);
            Assert.Equal(onScreen, viewModel.Snapshot.Values);

            viewModel.Settings.BarBorderColor = new RgbaColor(90, 90, 200, 128);
            Assert.Equal(onScreen, viewModel.Snapshot.Values);
        }
    }

    [Fact]
    public async Task ПравкаЦвета_ПриВоспроизведении_НеСдвигаетСтолбики()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedEarlyTone.Path);
            viewModel.TogglePlayCommand.Execute(null);
            player.AdvanceTo(TimeSpan.FromSeconds(0.6d));
            viewModel.UpdatePlayback();
            var onScreen = viewModel.Snapshot.Values;

            // Между правками позиция уехала вперёд: превью показывает то, что на экране,
            // а момент догонит ближайший тик.
            viewModel.Settings.BarFillColor = new RgbaColor(10, 200, 90);
            player.AdvanceTo(TimeSpan.FromSeconds(0.7d));
            viewModel.Settings.BackgroundColor = new RgbaColor(1, 2, 3);
            player.AdvanceTo(TimeSpan.FromSeconds(0.8d));
            viewModel.Settings.CenterLineColor = new RgbaColor(4, 5, 6, 200);

            Assert.Equal(onScreen, viewModel.Snapshot.Values);
        }
    }

    [Fact]
    public async Task ПравкаШириныСтолбика_НеДвигаетСпектр()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedEarlyTone.Path);

            // Тон кончился на 0,5 с, но сглаживание держит хвост около 0,8.
            viewModel.EndScrub(0.6d);
            var before = viewModel.Snapshot.Values;

            // Число столбиков меняется, наполнение то же: спектр не пересчитывается, поэтому
            // высота остаётся хвостом. Пересчёт сдвинул бы её ещё на шаг сглаживания.
            viewModel.Settings.BarWidth = 40;

            Assert.NotEqual(before.Length, viewModel.Snapshot.Values.Length);
            Assert.Contains(viewModel.Snapshot.Values, value => value > 0.7f);
        }
    }

    [Fact]
    public async Task ПравкаСпада_НеСдвигаетСтолбики()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedEarlyTone.Path);
            viewModel.EndScrub(0.6d);
            var onScreen = viewModel.Snapshot.Values;

            // Атака и спад описывают следующий кадр, а не текущий: правка любого из них не
            // должна пересчитывать показанное.
            viewModel.Settings.Release = 0.6f;
            Assert.Equal(onScreen, viewModel.Snapshot.Values);

            viewModel.Settings.Attack = 0.9f;
            Assert.Equal(onScreen, viewModel.Snapshot.Values);
        }
    }

    [Fact]
    public async Task ПовторныйКадрНаТойЖеПозиции_ПродолжаетДвигаться()
    {
        var (viewModel, player, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedEarlyTone.Path);
            viewModel.TogglePlayCommand.Execute(null);

            // Позиция плеера прыгает по прочитанным блокам звука, а таймер бьёт чаще: между
            // прыжками кадров много с одним моментом, и они обязаны двигать картинку.
            player.AdvanceTo(TimeSpan.FromSeconds(0.6d));
            viewModel.UpdatePlayback();
            var first = viewModel.Snapshot.Values;

            viewModel.UpdatePlayback();
            var second = viewModel.Snapshot.Values;

            Assert.Contains(first, value => value > TailBar);
            Assert.True(
                second.Max() < first.Max(),
                "Повторный кадр на том же моменте должен вести столбики к спектру дальше.");
        }
    }

    [Fact]
    public async Task ПравкаНастроек_НеОбнуляетСглаживание()
    {
        var (viewModel, _, directory) = Create();
        using (directory)
        {
            await viewModel.LoadFileAsync(SharedEarlyTone.Path);

            // Тон кончился на 0,5 с, но сглаживание держит хвост: столбики ещё ненулевые.
            viewModel.EndScrub(0.6d);
            Assert.Contains(viewModel.Snapshot.Values, value => value > TailBar);

            // Усиление входит в спектр, поэтому кадр пересчитывается - но сглаживание должно
            // продолжиться с хвоста, а не начаться заново сырым нулём.
            viewModel.Settings.GainDb = 6d;

            Assert.Contains(viewModel.Snapshot.Values, value => value > TailBar);
        }
    }
}