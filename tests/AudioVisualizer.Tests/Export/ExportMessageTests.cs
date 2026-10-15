using System.Text;
using AudioVisualizer.Core.Export;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Тексты ошибок выгрузки. Проверяются без ffmpeg: всё, что удастся узнать о сбое, попадает
/// именно в этот текст, и больше показать нечего.
/// </summary>
public class ExportMessageTests
{
    private const string Command = @"C:\tools\ffmpeg.exe -y -c:a aac -aac_coder nmr -shortest";

    [Theory]
    [InlineData("nmr")]
    [InlineData("twoloop")]
    [InlineData("fast")]
    public void Застой_НазываетКодировщикНаКоторомСлучился(string coder)
    {
        // Зависнуть может любой из трёх: проверено это только на одном треке, поэтому
        // сообщение обязано называть тот, на котором случилось, а не зашитый в текст.
        var message = Mp4Exporter.DescribeStall(coder, new StringBuilder(), Command);

        Assert.Contains($"aac ({coder})", message, StringComparison.Ordinal);
        Assert.Contains("перестал читать кадры", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nmr")]
    [InlineData("twoloop")]
    [InlineData("fast")]
    public void Застой_ПеречисляетОстальныеКодировщикиКакВарианты(string coder)
    {
        var message = Mp4Exporter.DescribeStall(coder, new StringBuilder(), Command);

        Assert.Contains("Смена кодировщика", message, StringComparison.Ordinal);

        foreach (var other in ExportSettings.AacCoders.Where(c => !string.Equals(c, coder, StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Contains(other, message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Застой_ДописываетСловаFfmpeg()
    {
        // Хвост stderr - всё, что удалось узнать от ffmpeg.
        var errors = new StringBuilder("qavg: nan\nConversion failed\n");

        var message = Mp4Exporter.DescribeStall("nmr", errors, Command);

        Assert.Contains("Conversion failed", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Застой_ДописываетКомандуFfmpeg()
    {
        // Команда нужна для повтора руками. Раньше она лежала в файле рядом с роликом, но
        // файл оставался в папке после выгрузки, и повторить было нечем.
        var message = Mp4Exporter.DescribeStall("nmr", new StringBuilder(), Command);

        Assert.Contains("Команда ffmpeg:", message, StringComparison.Ordinal);
        Assert.Contains("-aac_coder nmr", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Застой_КомандаИдётПослеСловFfmpeg()
    {
        var errors = new StringBuilder("Conversion failed\n");

        var message = Mp4Exporter.DescribeStall("nmr", errors, Command);

        // Порядок важен для чтения: сначала что сказал ffmpeg, потом чем он запускался.
        Assert.True(
            message.IndexOf("Conversion failed", StringComparison.Ordinal)
            < message.IndexOf("Команда ffmpeg:", StringComparison.Ordinal));
    }

    [Fact]
    public void Застой_НеВыдумываетСловаFfmpeg()
    {
        // Пустой stderr не должен оставлять в тексте лишний перенос строки перед командой.
        var message = Mp4Exporter.DescribeStall("nmr", new StringBuilder(), Command);

        Assert.DoesNotContain(Environment.NewLine + Environment.NewLine + Environment.NewLine, message, StringComparison.Ordinal);
    }
}