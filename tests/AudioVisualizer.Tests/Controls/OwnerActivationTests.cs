using System.Windows;
using AudioVisualizer.App;

namespace AudioVisualizer.Tests.Controls;

/// <summary>
/// Возврат переднего плана окну-владельцу после закрытия окна сообщения.
/// </summary>
/// <remarks>
/// Сама активация проверяется только вживую: нужен экран, а offscreen-рендер передний план
/// не показывает. Здесь закрывается решение - владельца трогать нельзя, чтобы закрытое или
/// неродившееся окно не роняло приложение из обработчика закрытия окна.
/// </remarks>
[Collection(WpfUi.Name)]
public class OwnerActivationTests
{
    [Fact]
    public void БезВладельца_НичегоНеПроисходит() => Sta.Run(() =>
    {
        var activated = new List<Window>();

        OwnerActivation.Restore(null, activated.Add);

        Assert.Empty(activated);
    });

    [Fact]
    public void НепоказанноеОкно_НеАктивируется() => Sta.Run(() =>
    {
        var window = new MessageWindow();
        var activated = new List<Window>();

        OwnerActivation.Restore(window, activated.Add);

        // Окно, которому владельца не показали, активировать нельзя: ни переднего плана
        // у него нет, ни окна на экране.
        Assert.Empty(activated);
    });

    [Fact]
    public void ЗакрытоеОкно_НеАктивируется() => Sta.Run(() =>
    {
        var window = new MessageWindow();
        window.Close();

        var activated = new List<Window>();

        // Владельца закрыли, пока было открыто окно сообщения: активировать уже нечего,
        // и падать тут нельзя - приложение закрывается.
        OwnerActivation.Restore(window, activated.Add);

        Assert.Empty(activated);
    });
}
