using AudioVisualizer.App.ViewModels;

namespace AudioVisualizer.Tests.ViewModels;

public class AudioFileFilterTests
{
    // WPF разбирает строку фильтра сам и бросает ArgumentException на клике по кнопке.
    // Правило проверяем здесь: строка состоит из чётного числа частей, разделённых "|",
    // и каждая пара - непустое описание и непустой шаблон.
    [Fact]
    public void Фильтр_СостоитИзПарОписаниеИШаблон()
    {
        var parts = MainViewModel.AudioFileFilter.Split('|');

        Assert.Equal(0, parts.Length % 2);
        Assert.NotEmpty(parts);
    }

    [Fact]
    public void Фильтр_ОписаниеИШаблонНеПустые()
    {
        var parts = MainViewModel.AudioFileFilter.Split('|');

        for (var i = 0; i < parts.Length; i += 2)
        {
            Assert.False(string.IsNullOrWhiteSpace(parts[i]));
            Assert.False(string.IsNullOrWhiteSpace(parts[i + 1]));
        }
    }

    [Fact]
    public void Фильтр_РасширенияРазделеныТочкойСЗапятой()
    {
        var parts = MainViewModel.AudioFileFilter.Split('|');

        for (var i = 1; i < parts.Length; i += 2)
        {
            foreach (var pattern in parts[i].Split(';'))
            {
                Assert.Matches(@"^(\*\.\*|\*\.[A-Za-z0-9]+)$", pattern);
            }
        }
    }

    [Fact]
    public void Фильтр_СодержитОсновныеАудиоформаты()
    {
        Assert.Contains("*.mp3", MainViewModel.AudioFileFilter);
        Assert.Contains("*.wav", MainViewModel.AudioFileFilter);
        Assert.Contains("*.*", MainViewModel.AudioFileFilter);
    }
}