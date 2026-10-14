using System.Windows;
using AudioVisualizer.App.ViewModels;

namespace AudioVisualizer.App;

/// <summary>
/// Окно приложения. Модель представления создаётся здесь же: у окна нет состояния помимо неё,
/// а в тестах модель создаётся напрямую и окно не требуется.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
        Closed += OnClosed;
    }

    // Настройки сохраняются при выходе, а не на каждое движение ползунка: иначе файл
    // переписывался бы десятки раз в секунду.
    private void OnClosed(object? sender, EventArgs args)
        => (DataContext as MainViewModel)?.SaveSettingsCommand.Execute(null);
}