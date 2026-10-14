using AudioVisualizer.Core.Audio;

namespace AudioVisualizer.Tests.Audio;

public class FfmpegLocatorTests
{
    [Fact]
    public void Locate_ПутьИзНастроек_ПриоритетнееПеременнойPath()
    {
        var located = FfmpegLocator.Locate(
            configuredPath: @"C:\tools\ffmpeg.exe",
            pathVariable: @"C:\other",
            fileExists: path => path == @"C:\tools\ffmpeg.exe");

        Assert.Equal(@"C:\tools\ffmpeg.exe", located);
    }

    [Fact]
    public void Locate_НаходитВПеременнойPath()
    {
        var located = FfmpegLocator.Locate(
            configuredPath: null,
            pathVariable: @"C:\first;" + @"C:\second",
            fileExists: path => path == @"C:\second\ffmpeg.exe");

        Assert.Equal(@"C:\second\ffmpeg.exe", located);
    }

    [Fact]
    public void Locate_НесуществующийНастроенныйПуть_ПродолжаетИскатьВPath()
    {
        var located = FfmpegLocator.Locate(
            configuredPath: @"C:\missing\ffmpeg.exe",
            pathVariable: @"C:\tools",
            fileExists: path => path == @"C:\tools\ffmpeg.exe");

        Assert.Equal(@"C:\tools\ffmpeg.exe", located);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Locate_ПустойPath_ВозвращаетNull(string? pathVariable)
        => Assert.Null(FfmpegLocator.Locate(null, pathVariable, _ => true));

    [Fact]
    public void Locate_НесуществующийФайл_ВозвращаетNull()
        => Assert.Null(FfmpegLocator.Locate(null, @"C:\tools", _ => false));

    [Fact]
    public void Locate_НекорректнаяЗаписьPath_НеЛомаетПоиск()
    {
        var located = FfmpegLocator.Locate(
            configuredPath: null,
            pathVariable: "\";" + @"C:\tools",
            fileExists: path => path == @"C:\tools\ffmpeg.exe");

        Assert.Equal(@"C:\tools\ffmpeg.exe", located);
    }

    [Fact]
    public void EnsureFound_НайденныйFfmpeg_ВозвращаетПуть()
    {
        using var executable = new TemporaryAudioFile([1, 2, 3], extension: ".exe");
        var locator = new FfmpegLocator(executable.Path, string.Empty);

        Assert.Equal(executable.Path, locator.EnsureFound());
    }

    [Fact]
    public void EnsureFound_БезFfmpeg_ВыбрасываетПонятнуюОшибку()
    {
        var locator = new FfmpegLocator(@"C:\missing\ffmpeg.exe", string.Empty);

        var exception = Assert.Throws<FfmpegNotFoundException>(() => locator.EnsureFound());

        Assert.Contains("ffmpeg", exception.Message);
    }
}