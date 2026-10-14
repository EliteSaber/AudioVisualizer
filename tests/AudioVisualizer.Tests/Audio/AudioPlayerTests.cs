using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Поведение плеера на подменах устройств: без звуковой карты проверяются ровно те
/// состояния, из-за которых ломалась перемотка и возобновление после паузы.
/// </summary>
public class AudioPlayerTests
{
    private static readonly TimeSpan TrackDuration = TimeSpan.FromSeconds(100d);

    [Fact]
    public void Play_ОткрываетТрек_ИЗапускаетВывод()
    {
        var harness = new Harness();

        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(1, harness.TracksCreated);
        Assert.Equal(1, harness.Output.InitCount);
        Assert.Equal(1, harness.Output.PlayCount);
        Assert.True(harness.Player.IsPlaying);
    }

    [Fact]
    public void Play_ПоТомуЖеПути_НеПересоздаётТрек()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Pause();
        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(1, harness.TracksCreated);
        Assert.Equal(2, harness.Output.PlayCount);
    }

    [Fact]
    public void Play_ПоДругомуПути_ОткрываетНовыйТрек()
    {
        var harness = new Harness();
        harness.Player.Play("первый.mp3", 1d);

        harness.Player.Play("второй.mp3", 1d);

        Assert.Equal(2, harness.TracksCreated);
    }

    [Fact]
    public void Pause_ЗапоминаетПозицию_Play_ВозобновляетСНеё()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);
        harness.Track.Position = TimeSpan.FromSeconds(42d);

        harness.Player.Pause();
        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(TimeSpan.FromSeconds(42d), harness.Track.Position);
        Assert.Equal(TimeSpan.FromSeconds(42d), harness.Player.Position);
    }

    [Fact]
    public void ДваЦиклаPlayPause_ВозобновлениеСПоследнейПаузы()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Track.Position = TimeSpan.FromSeconds(10d);
        harness.Player.Pause();
        harness.Player.Play("трек.mp3", 1d);

        harness.Track.Position = TimeSpan.FromSeconds(20d);
        harness.Player.Pause();
        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(TimeSpan.FromSeconds(20d), harness.Player.Position);
        Assert.Equal(1, harness.TracksCreated);
    }

    [Fact]
    public void SeekДоЗапуска_Play_СтартуетСЭтойПозиции()
    {
        var harness = new Harness();

        harness.Player.Seek(TimeSpan.FromSeconds(30d));
        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(TimeSpan.FromSeconds(30d), harness.Track.Position);
    }

    [Fact]
    public void ПеремоталПолзунком_Пауза_Play_ВозвратВТочкуПолзунка()
    {
        var harness = new Harness();
        harness.Player.Seek(TimeSpan.FromSeconds(30d));
        harness.Player.Play("трек.mp3", 1d);
        harness.Track.Position = TimeSpan.FromSeconds(35d);

        harness.Player.Pause();
        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(TimeSpan.FromSeconds(35d), harness.Player.Position);
        Assert.Equal(1, harness.TracksCreated);
    }

    [Fact]
    public void Seek_СохраняетВоспроизведение()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Seek(TimeSpan.FromSeconds(15d));

        Assert.True(harness.Player.IsPlaying);
    }

    [Fact]
    public void Seek_ОграничиваетПозициюДлительностью()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Seek(TimeSpan.FromSeconds(500d));

        Assert.Equal(TrackDuration, harness.Track.Position);
    }

[Fact]
    public void SeekВоВремяВоспроизведения_НеОстанавливаетВыводНавсегда()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Seek(TimeSpan.FromSeconds(15d));

        Assert.True(harness.Player.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(15d), harness.Track.Position);
    }

    [Fact]
    public void SeekВоВремяВоспроизведения_ПереоткрываетТрек()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Seek(TimeSpan.FromSeconds(15d));

        // Открытый ридер отдаёт звук из буферов, набранных до перемотки, поэтому
        // при перемотке на живом потоке файл открывается заново.
        Assert.Equal(2, harness.TracksCreated);
        Assert.Equal(TimeSpan.FromSeconds(15d), harness.Track.Position);
        Assert.True(harness.Player.IsPlaying);
    }

    [Fact]
    public void SeekПокаНаПаузе_ТрекНеПереоткрывает()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);
        harness.Player.Pause();

        harness.Player.Seek(TimeSpan.FromSeconds(15d));

        // На паузе звука нет, сдвигается тот же трек: переоткрытие вызывало щелчок
        // ради ничего.
        Assert.Equal(1, harness.TracksCreated);
        Assert.Equal(TimeSpan.FromSeconds(15d), harness.Track.Position);
    }

    [Fact]
    public void SeekВоВремяВоспроизведения_СохраняетГромкость()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 0.4f);

        harness.Player.Seek(TimeSpan.FromSeconds(15d));

        Assert.Equal(0.4f, harness.Track.Volume);
    }

    [Fact]
    public void SeekВоВремяВоспроизведения_ПродолжаетТотЖеФайл()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Seek(TimeSpan.FromSeconds(15d));
        harness.Player.Pause();
        harness.Player.Play("трек.mp3", 1d);

        // После перемотки файл остался открытым: возобновление не должно открывать его
        // в третий раз.
        Assert.Equal(2, harness.TracksCreated);
    }

    [Fact]
    public void SeekДоЗапуска_ЗапоминаетПозицию()
    {
        var harness = new Harness();

        harness.Player.Seek(TimeSpan.FromSeconds(12d));

        Assert.Equal(TimeSpan.FromSeconds(12d), harness.Player.Position);
    }

    [Fact]
    public void SeekОтрицательный_ВедётВНачало()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Seek(TimeSpan.FromSeconds(-5d));

        Assert.Equal(TimeSpan.Zero, harness.Track.Position);
    }

    [Fact]
    public void StopPlayback_СбрасываетПозициюВНачало()
    {
        var harness = new Harness();
        harness.Player.Seek(TimeSpan.FromSeconds(30d));
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.StopPlayback();

        Assert.Equal(TimeSpan.Zero, harness.Player.Position);
        Assert.False(harness.Player.IsPlaying);
    }

    [Fact]
    public void StopPlayback_ОсвобождаетУстройства()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.StopPlayback();

        Assert.True(harness.Track.IsDisposed);
        Assert.True(harness.Output.IsDisposed);
    }

    [Fact]
    public void StopPlayback_ПотомPlay_ОткрываетНовыйТрек()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);
        harness.Player.StopPlayback();

        harness.Player.Play("трек.mp3", 1d);

        Assert.Equal(2, harness.TracksCreated);
    }

    [Fact]
    public void PauseДоЗапуска_НеПадает()
    {
        var harness = new Harness();

        harness.Player.Pause();

        Assert.False(harness.Player.IsPlaying);
    }

    [Fact]
    public void PlayСПустымПутём_Отклоняется()
    {
        var harness = new Harness();

        Assert.Throws<ArgumentException>(() => harness.Player.Play("   ", 1d));
    }

    [Fact]
    public void ПовторныйDispose_НеПадает()
    {
        var harness = new Harness();
        harness.Player.Play("трек.mp3", 1d);

        harness.Player.Dispose();
        harness.Player.Dispose();
    }

    private sealed class Harness
    {
        private readonly List<FakeAudioTrack> _tracks = [];
        private readonly List<FakeAudioOutput> _outputs = [];

        public Harness() => Player = new AudioPlayer(CreateTrack, CreateOutput);

        public AudioPlayer Player { get; }

        public int TracksCreated => _tracks.Count;

        public int OutputsCreated => _outputs.Count;

        public FakeAudioTrack Track => _tracks[^1];

        public FakeAudioOutput Output => _outputs[^1];

        private FakeAudioTrack CreateTrack(string path)
        {
            var track = new FakeAudioTrack(TrackDuration);
            _tracks.Add(track);

            return track;
        }

        private FakeAudioOutput CreateOutput()
        {
            var output = new FakeAudioOutput();
            _outputs.Add(output);

            return output;
        }
    }
}