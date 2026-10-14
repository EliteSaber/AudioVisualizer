using System.Windows;
using System.Windows.Controls;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Строка настройки: подпись слева, значение справа. Вынесена отдельным элементом, чтобы в разметке
/// панели строка занимала одну строку, а не восемь.
/// </summary>
public sealed class SettingRow : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SettingRow),
        new PropertyMetadata(string.Empty));

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}