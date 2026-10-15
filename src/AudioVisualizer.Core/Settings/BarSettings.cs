namespace AudioVisualizer.Core.Settings;

/// <summary>Форма столбиков. Число столбиков выводится из ширины и промежутка, а не хранится.</summary>
public sealed record BarSettings
{
    /// <summary>Ширина столбика в пикселях кадра.</summary>
    public int Width { get; init; } = 8;

    /// <summary>Промежуток между соседними столбиками.</summary>
    public int Spacing { get; init; } = 4;

    /// <summary>Радиус скругления углов в пикселях.</summary>
    public int CornerRadius { get; init; } = 4;

    public CornerStyle CornerStyle { get; init; } = CornerStyle.OuterRounded;

    /// <summary>Толщина границы столбика в пикселях.</summary>
    public double BorderWidth { get; init; } = 1d;

    public BarFillMode FillMode { get; init; } = BarFillMode.VerticalGradient;

    /// <summary>Отступ от края кадра до первого столбика.</summary>
    public int Margin { get; init; } = 48;

    /// <summary>Зазор между центральной линией и краями столбиков.</summary>
    public int CenterGap { get; init; } = 4;

    /// <summary>Рисовать нижнюю половину. По умолчанию столбики зеркальны.</summary>
    public bool Mirror { get; init; } = true;

    /// <summary>
    /// Время затухания следа в миллисекундах: сколько держится призрак прежней высоты столбика.
    /// Ноль - следа нет. Задано временем, а не кадрами, поэтому длина следа одинакова в превью
    /// и в ролике любой частоты.
    /// </summary>
    /// <remarks>
    /// Не то же самое, что <see cref="SpectrumSettings.Release"/>: спад замедляет уменьшение
    /// высоты, а след рисует призрак того, где столбик был.
    /// </remarks>
    public double TrailDecayMs { get; init; }

    /// <summary>
    /// Плотность следа от 0 до 1: во сколько раз непрозрачность следа меньше непрозрачности
    /// столбика. По умолчанию чуть меньше единицы, иначе след у вершины совпадает со столбиком
    /// по цвету и границы между ними не видно.
    /// </summary>
    public double TrailDensity { get; init; } = 0.8d;
}