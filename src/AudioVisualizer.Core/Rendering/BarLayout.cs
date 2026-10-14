using System.Windows;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Core.Rendering;

/// <summary>
/// Геометрия столбиков: сколько их помещается в кадр и где лежит каждый из них.
/// Считается без WPF-рисования, поэтому проверяется тестами без рендера.
/// </summary>
/// <remarks>
/// Столбики расходятся от центральной линии: верхняя половина растёт от центра вверх,
/// нижняя зеркально вниз. Между ними остаётся зазор <see cref="BarSettings.CenterGap"/>.
/// </remarks>
public sealed class BarLayout
{
    public BarLayout(double width, double height, BarSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;

        var usableWidth = Math.Max(0d, width - (2d * settings.Margin));
        var step = settings.Width + (double)settings.Spacing;

        // Последний столбик считается вместе с промежутком после него: иначе он уезжает за край.
        BarCount = step > 0 ? (int)Math.Floor((usableWidth + settings.Spacing) / step) : 0;
        BarCount = Math.Max(0, BarCount);

        BarWidth = settings.Width;
        ContentWidth = Math.Max(0d, (BarCount * step) - settings.Spacing);
        ContentLeft = settings.Margin + ((usableWidth - ContentWidth) / 2d);

        CenterY = height / 2d;
        HalfGap = Math.Max(0d, settings.CenterGap / 2d);
        MaxHalfHeight = Math.Max(0d, CenterY - HalfGap - settings.Margin);
    }

    public BarSettings Settings { get; }

    public int BarCount { get; }

    public double BarWidth { get; }

    public double ContentLeft { get; }

    public double ContentWidth { get; }

    public double CenterY { get; }

    /// <summary>Половина зазора между верхней и нижней половинами столбика.</summary>
    public double HalfGap { get; }

    /// <summary>Высота столбика при значении 1: от центра до отступа от края кадра.</summary>
    public double MaxHalfHeight { get; }

    public bool IsMirrored => Settings.Mirror;

    public double BarLeft(int index) => ContentLeft + (index * (Settings.Width + (double)Settings.Spacing));

    /// <summary>Верхняя половина столбика. Значение 0 даёт пустой прямоугольник.</summary>
    public Rect GetUpperBar(int index, double value)
    {
        ValidateIndex(index);

        var height = HalfHeightOf(value);

        return new Rect(BarLeft(index), CenterY - HalfGap - height, BarWidth, height);
    }

    /// <summary>Нижняя половина столбика: зеркало верхней относительно центральной линии.</summary>
    public Rect GetLowerBar(int index, double value)
    {
        ValidateIndex(index);

        var height = HalfHeightOf(value);

        return new Rect(BarLeft(index), CenterY + HalfGap, BarWidth, height);
    }

    /// <summary>Прямоугольник центральной линии: зазор между половинами, залитый её цветом.</summary>
    public Rect GetCenterLine() => new(ContentLeft, CenterY - HalfGap, ContentWidth, HalfGap * 2d);

    private double HalfHeightOf(double value) => Math.Clamp(value, 0d, 1d) * MaxHalfHeight;

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= BarCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Номер столбика вне диапазона 0..{BarCount - 1}.");
        }
    }
}