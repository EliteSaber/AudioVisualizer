using AudioVisualizer.Core.Export;

namespace AudioVisualizer.Tests.Export;

/// <summary>
/// Зонд видеокарты без ffmpeg: отвечает заранее заданным списком и считает обращения.
/// </summary>
/// <remarks>
/// Общий для проверок модели представления и привязки: свой файл и internal, потому что нужен
/// обоим наборам тестов, а копия подставного зонда в каждом месте расходилась бы с ним так же,
/// как расходились бы две копии настоящего кода.
/// </remarks>
internal sealed class FakeEncoderDetector : IVideoEncoderDetector
{
    private readonly IReadOnlyList<VideoEncoderSupport>? _found;
    private readonly bool _ffmpegAvailable;
    private readonly Exception? _failure;

    public FakeEncoderDetector(IReadOnlyList<VideoEncoderSupport> found, bool ffmpegAvailable = true)
    {
        _found = found;
        _ffmpegAvailable = ffmpegAvailable;
    }

    public FakeEncoderDetector(Exception failure)
    {
        _failure = failure;
    }

    public int Calls { get; private set; }

    /// <summary>Путь, который последним спросили: им проверяется, что уходит в зонд путь из настроек.</summary>
    public string? LastPath { get; private set; }

    /// <summary>Кодек, который последним спросили.</summary>
    public string? LastCodec { get; private set; }

    public Task<VideoEncoderDetection> DetectAsync(
        string? ffmpegPath,
        string videoCodec,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastPath = ffmpegPath;
        LastCodec = videoCodec;

        return _failure is not null
            ? Task.FromException<VideoEncoderDetection>(_failure)
            : Task.FromResult(new VideoEncoderDetection(_found ?? [], _ffmpegAvailable));
    }
}