using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Выбор цвета: образец, значение в hex и всплывающая панель каналов. Своя реализация вместо
/// диалога WinForms, чтобы приложение осталось чистым WPF.
/// </summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(RgbaColor),
        typeof(ColorPicker),
        new FrameworkPropertyMetadata(default(RgbaColor), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    private bool _updating;

    public ColorPicker()
    {
        InitializeComponent();

        ColorPopup.PlacementTarget = Swatch;
        ColorPopup.Opened += OnPopupOpened;
        ShowColor(Color);
    }

    public RgbaColor Color
    {
        get => (RgbaColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((ColorPicker)sender).ShowColor((RgbaColor)args.NewValue);

    private void ShowColor(RgbaColor color)
    {
        // Каналы и текст обновляются под защитой: правка ползунка меняет Color,
        // а та возвращается сюда же.
        _updating = true;
        try
        {
            Swatch.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            HexLabel.Text = color.ToHex();
            HexInput.Text = color.ToHex();

            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            AlphaSlider.Value = color.A;
            TransparentCheckBox.IsChecked = color.A == 0;
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (_updating)
        {
            return;
        }

        Color = new RgbaColor(
            (byte)RedSlider.Value,
            (byte)GreenSlider.Value,
            (byte)BlueSlider.Value,
            (byte)AlphaSlider.Value);
    }

    private void OnTransparentChecked(object sender, RoutedEventArgs args)
    {
        if (_updating || TransparentCheckBox.IsChecked != true)
        {
            return;
        }

        _updating = true;
        try
        {
            AlphaSlider.Value = 0;
        }
        finally
        {
            _updating = false;
        }

        Color = new RgbaColor((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value, 0);
    }

    private void OnHexKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ApplyHex();
        }
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs args) => ApplyHex();

    private void OnSwatchClick(object sender, MouseButtonEventArgs args) => ColorPopup.IsOpen = !ColorPopup.IsOpen;

    // Фокус переносим внутрь панели: пока он снаружи, Esc уходит элементу, на котором и был,
    // и до обработчика не доходит.
    private void OnPopupOpened(object? sender, EventArgs args) => HexInput.Focus();

    // Попап открывается по отпусканию кнопки мыши, а не по нажатию: открытый на нажатии,
    // он ловит отпускание как клик снаружи и тут же закрывается.
    private void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && ColorPopup.IsOpen)
        {
            ColorPopup.IsOpen = false;
            args.Handled = true;
        }
    }

    private void ApplyHex()
    {
        if (RgbaColor.TryParse(HexInput.Text, out var parsed))
        {
            Color = parsed;
            return;
        }

        // Незаданный текст возвращаем к текущему цвету: молча оставлять опечатку нельзя.
        ShowColor(Color);
    }
}