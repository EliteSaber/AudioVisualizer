namespace AudioVisualizer.App.ViewModels;

/// <summary>
/// Пункт выпадающего списка: значение и подпись для интерфейса.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> возвращает подпись, а не состав записи. Любой путь отрисовки,
/// который не нашёл шаблон, выводит строковое представление, и пользователь видел бы
/// "Choice { Value = OuterRounded, Title = ... }" вместо "Скруглённые снаружи".
/// </remarks>
public sealed record Choice<T>(T Value, string Title)
{
    public override string ToString() => Title;
}