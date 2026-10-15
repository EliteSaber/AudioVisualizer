using AudioVisualizer.Core.Export;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Сторож застоя. Время подставляется своё: иначе проверка ждала бы настоящие полминуты.
/// </summary>
public class StallWatchTests
{
    [Fact]
    public void БезПрогресса_ЗастойНаступает()
    {
        var now = TimeSpan.Zero;
        var watch = new StallWatch(TimeSpan.FromSeconds(30), () => now);

        now = TimeSpan.FromSeconds(29.9);

        Assert.False(watch.HasStalled);

        now = TimeSpan.FromSeconds(30);

        Assert.True(watch.HasStalled);
    }

    [Fact]
    public void ОтсчётОтМоментаСоздания_ДоПервогоКадраНеЗастой()
    {
        // Выгрузка, которая не успела прочитать ни одного кадра, ещё не зависла: иначе
        // сторож срабатывал бы на любом медленном старте.
        var now = TimeSpan.Zero;
        var watch = new StallWatch(TimeSpan.FromSeconds(30), () => now);

        now = TimeSpan.FromSeconds(10);

        Assert.False(watch.HasStalled);
    }

    [Fact]
    public void ReportProgress_СбрасываетОтсчёт()
    {
        var now = TimeSpan.Zero;
        var watch = new StallWatch(TimeSpan.FromSeconds(30), () => now);

        now = TimeSpan.FromSeconds(25);
        watch.ReportProgress();

        now = TimeSpan.FromSeconds(45);

        Assert.False(watch.HasStalled);
    }

    [Fact]
    public void Застой_НеРазжимаетсяПрогрессом()
    {
        // Зависание нельзя отменить пришедшим кадром: если ffmpeg уже списан как зависший,
        // выгрузка должна сообщить об этом, а не пойти дальше.
        var now = TimeSpan.Zero;
        var watch = new StallWatch(TimeSpan.FromSeconds(30), () => now);

        now = TimeSpan.FromSeconds(31);

        Assert.True(watch.HasStalled);

        watch.ReportProgress();
        now = TimeSpan.FromSeconds(31.5);

        Assert.True(watch.HasStalled);
    }

    [Fact]
    public void НулевоеВремяОжидания_Отклоняется()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StallWatch(TimeSpan.Zero));
    }
}