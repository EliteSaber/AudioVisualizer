namespace AudioVisualizer.Core.Settings;

/// <summary>Параметры выгрузки видео.</summary>
public sealed record ExportSettings
{
    public int Width { get; init; } = 1920;

    public int Height { get; init; } = 1080;

    public int FramesPerSecond { get; init; } = 60;

    /// <summary>Качество x264: меньше - лучше и медленнее. 18 - почти без потерь.</summary>
    public int Crf { get; init; } = 18;

    /// <summary>Пресет x264: ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow.</summary>
    public string CodecPreset { get; init; } = "medium";

    public int AudioBitrateKbps { get; init; } = 192;

    /// <summary>Путь к ffmpeg.exe. Пусто - искать в PATH.</summary>
    public string? FfmpegPath { get; init; }
}