using System.Diagnostics;
using System.IO;
using System.Windows;
using AudioVisualizer.App.ViewModels;

namespace AudioVisualizer.App;

/// <summary>
/// Сообщение отдельным окном: и об ошибке, и об удачной выгрузке.
/// </summary>
/// <remarks>
/// У ошибки в тексте лежат слова ffmpeg и сама команда. Раньше то и другое писалось в журнал
/// рядом с роликом, но файл оставался в папке после каждой выгрузки, и разбор шёл слепым.
///
/// Содержимое задаётся присваиванием, а не привязкой: у окна один выстрел, и DataContext тут
/// был бы лишним слоем.
///
/// Открывающее действие передаётся снаружи, а не сделано виртуальным методом: наследника
/// нельзя создать, WPF ищет разметку в сборке runtime-типа, а у подкласса в тестах она своя.
/// </remarks>
public partial class MessageWindow : Window
{
    private readonly Action<string> _openFolder;
    private string? _folder;

    /// <summary>Пустой конструктор нужен разметке, а тестам - чтобы собрать окно и не показывать.</summary>
    public MessageWindow()
        : this(null, null)
    {
    }

    public MessageWindow(Notice notice)
        : this(notice, null)
    {
    }

    internal MessageWindow(Notice? notice, Action<string>? openFolder)
    {
        InitializeComponent();

        _openFolder = openFolder ?? OpenInExplorer;

        if (notice is null)
        {
            return;
        }

        Title = notice.Title;
        HeadingBlock.Text = notice.Title;
        MessageBox.Text = notice.Message;

        // Кнопка появляется только когда папка действительно есть: у ошибки её нет вовсе,
        // ролик в ошибке не появился.
        if (string.IsNullOrWhiteSpace(notice.FolderPath) || !Directory.Exists(notice.FolderPath))
        {
            return;
        }

        _folder = notice.FolderPath;
        FolderButton.Visibility = Visibility.Visible;
    }

    public static void Show(Window owner, Notice notice)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(notice);

        var window = new MessageWindow(notice) { Owner = owner };

        window.ShowDialog();
    }

    private static void OpenInExplorer(string path)
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        startInfo.ArgumentList.Add(path);

        Process.Start(startInfo);
    }

    private void OnFolderClick(object sender, RoutedEventArgs args)
    {
        if (_folder is not null)
        {
            _openFolder(_folder);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs args) => Close();
}