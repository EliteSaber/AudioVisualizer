namespace AudioVisualizer.Core.Settings;

/// <summary>Цвета визуализации.</summary>
public sealed record ColorSettings
{
    public RgbaColor Background { get; init; } = new(18, 18, 24);

    /// <summary>Заливка у центра столбика.</summary>
    public RgbaColor BarFill { get; init; } = new(56, 132, 255);

    /// <summary>Заливка у внешних краёв. Используется, только когда включён <see cref="BarSettings.FillMode"/> с градиентом.</summary>
    public RgbaColor BarFillSecondary { get; init; } = new(255, 96, 168);

    public RgbaColor BarBorder { get; init; } = RgbaColor.Transparent;

    /// <summary>Центральная линия, от которой столбики расходятся вверх и вниз.</summary>
    public RgbaColor CenterLine { get; init; } = new(255, 255, 255, 64);
}