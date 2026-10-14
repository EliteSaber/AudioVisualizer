using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AudioVisualizer.App.ViewModels;

namespace AudioVisualizer.App;

/// <summary>
/// Окно приложения. Модель представления создаётся здесь же: у окна нет состояния помимо неё,
/// а в тестах модель создаётся напрямую и окно не требуется.
/// </summary>
public partial class MainWindow : Window
{
    // Пока это true, значение ползунка выставляет программа, а не человек: без такой
    // метки собственное изменение ползунка не отличить от реакции на модель представления.
    private bool _settingSlider;

    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel();

        // Позиция выставляется в ползунок напрямую, а не привязкой: привязка затирала бы
        // значение, которое пользователь выбирает перетаскиванием. Подписка молчит, пока
        // идёт перемотка, поэтому ползунок остаётся там, куда его поставили.
        viewModel.PositionChanged += seconds =>
        {
            _settingSlider = true;
            try
            {
                PositionSlider.Value = seconds;
            }
            finally
            {
                _settingSlider = false;
            }
        };

        PositionSlider.ValueChanged += OnPositionSliderValueChanged;

        DataContext = viewModel;
        Closed += OnClosed;
    }

    // Настройки сохраняются при выходе, а не на каждое движение ползунка: иначе файл
    // переписывался бы десятки раз в секунду.
    private void OnClosed(object? sender, EventArgs args)
        => (DataContext as MainViewModel)?.SaveSettingsCommand.Execute(null);

    /// <summary>
    /// Пользователь тронул ползунок: пока он его держит, позицией владеет он, а не таймер,
    /// иначе бегунок возвращался бы назад на каждом кадре.
    /// </summary>
    private void OnPositionSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
    {
        if (!_settingSlider)
        {
            (DataContext as MainViewModel)?.BeginScrub();
        }
    }

    private void OnScrubMouseUp(object sender, MouseButtonEventArgs args)
    {
        // Значение берём из именованного ползунка, а не из sender: так проверка не зависит
        // от того, как WPF определит отправителя события.
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.EndScrub(PositionSlider.Value);
        }
    }
}