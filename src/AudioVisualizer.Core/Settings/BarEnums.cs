namespace AudioVisualizer.Core.Settings;

/// <summary>Как скруглены углы столбика.</summary>
public enum CornerStyle
{
    /// <summary>Прямые углы.</summary>
    Rectangular = 0,

    /// <summary>Скруглены все четыре угла.</summary>
    Rounded = 1,

    /// <summary>Скруглены только внешние углы, у центральной линии углы прямые.</summary>
    OuterRounded = 2
}

/// <summary>Как залит столбик.</summary>
public enum BarFillMode
{
    Solid = 0,

    /// <summary>Градиент от <see cref="ColorSettings.BarFill"/> у центра к <see cref="ColorSettings.BarFillSecondary"/> у краёв.</summary>
    VerticalGradient = 1
}