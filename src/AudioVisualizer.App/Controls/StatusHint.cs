using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AudioVisualizer.App.ViewModels;

namespace AudioVisualizer.App.Controls;

/// <summary>
/// Подсказка над строкой статуса: показывает подробности сообщения, которых в самой строке
/// нет. Привязка задаётся кодом, а не разметкой.
/// </summary>
/// <remarks>
/// Содержимое подсказки живёт в отдельном попапе, и <c>DataContext</c> элемента в него не
/// наследуется: простая <c>{Binding StatusDetail}</c> внутри подсказки молча даёт пустую.
/// Обход через <c>RelativeSource</c> и <c>PlacementTarget</c> тоже опирается на внутренности
/// WPF - приведение свойства и обход дерева попапа, - и на практике подсказка вышла пустым
/// квадратом: обе зависимости выглядели правдоподобно, но ни одна не проверялась. Здесь
/// источник указан прямо - это объект, а не путь по дереву попапа.
/// </remarks>
public static class StatusHint
{
    /// <summary>
    /// Ширина подсказки. Попап измеряется без ограничения, поэтому длинная строка ffmpeg без
    /// этой ширины уехала бы за край экрана.
    /// </summary>
    private const double HintWidth = 520d;

    /// <summary>Путь к подробностям: у надписи статус, у её <c>DataContext</c> - модель.</summary>
    private static readonly string DetailPath = $"DataContext.{nameof(MainViewModel.StatusDetail)}";

    /// <summary>
    /// Назначает <paramref name="owner"/> подсказку с подробностями сообщения.
    /// </summary>
    /// <remarks>
    /// Привязка создаётся на каждый вызов: у <see cref="Binding"/> один владелец, и та же
    /// привязка, заданная второй раз, уже занята первой.
    /// </remarks>
    public static void Attach(FrameworkElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var hint = new TextBlock
        {
            MaxWidth = HintWidth,
            TextWrapping = TextWrapping.Wrap
        };

        // Источник - сам владелец, а не его имя в разметке: привязка с готовым источником
        // начинает работать сразу, и положение владельца в дереве на неё не влияет.
        hint.SetBinding(TextBlock.TextProperty, new Binding(DetailPath)
        {
            Source = owner,
            Mode = BindingMode.OneWay
        });

        owner.ToolTip = hint;
    }
}