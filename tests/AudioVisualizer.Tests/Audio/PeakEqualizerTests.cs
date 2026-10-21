using AudioVisualizer.Core.Audio;
using AudioVisualizer.Core.Settings;

namespace AudioVisualizer.Tests.Audio;

/// <summary>
/// Настоящий эквалайзер: полосы с усилением считаются biquad-фильтрами, и проверяется
/// ровно то, что обещано - сколько децибел добавляет полоса и что происходит с сигналом
/// по краям диапазона.
/// </summary>
public class PeakEqualizerTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void НулевоеУсиление_НеМеняетСигнал()
    {
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var equalizer = new PeakEqualizer(SampleRate, 1, [new EqualizerBand(1000f, 0f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        // Полоса с нулевым усилением не строится вовсе: peaking с нулевым усилением
        // амплитуду пропускает, а фазу всё равно сдвигает.
        Assert.Equal(input, output);
        Assert.False(equalizer.HasEffect);
    }

    [Fact]
    public void ПлюсШестьДецибел_ДаётПриростГромкости()
    {
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var equalizer = new PeakEqualizer(SampleRate, 1, [new EqualizerBand(1000f, 6f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        // 10^(6/20) = 1.995: во столько раз должна вырасти среднеквадратичное отклонение.
        var ratio = TestTone.Rms(output, SampleRate / 2) / TestTone.Rms(input, SampleRate / 2);

        Assert.InRange(ratio, 1.8d, 2.2d);
        Assert.True(equalizer.HasEffect);
        Assert.True(equalizer.HasBoost);
    }

    [Fact]
    public void МинусШестьДецибел_УменьшаетГромкость()
    {
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var equalizer = new PeakEqualizer(SampleRate, 1, [new EqualizerBand(1000f, -6f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        var ratio = TestTone.Rms(output, SampleRate / 2) / TestTone.Rms(input, SampleRate / 2);

        Assert.InRange(ratio, 0.45d, 0.56d);
        Assert.False(equalizer.HasBoost);
    }

    [Fact]
    public void ПолосаВышеНайквиста_Пропускается()
    {
        // При 22050 Гц Найквист - это 11025 Гц, и полоса 16 кГц построить нельзя:
        // коэффициенты у неё вырожденные, и такой фильтр кладёт звук.
        var input = TestTone.Sine(22050, 1000f, 0.5f, 22050);
        var equalizer = new PeakEqualizer(22050, 1, [new EqualizerBand(16000f, 12f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        Assert.Equal(input, output);
        Assert.False(equalizer.HasEffect);
    }

    [Fact]
    public void НаВыходеНетНеконечностей()
    {
        var input = TestTone.Sine(SampleRate, 1000f, 0.9f, SampleRate);
        var equalizer = new PeakEqualizer(
            SampleRate,
            1,
            [new EqualizerBand(1000f, 12f), new EqualizerBand(2000f, 12f), new EqualizerBand(125f, 12f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        Assert.All(output, sample => Assert.True(float.IsFinite(sample), "На выходе появилось нечисловое значение."));
    }

    [Fact]
    public void СменаПолосНеСбрасываетСостояние()
    {
        // Постоянный сигнал единица: у peaking-фильтра усиление на нулевой частоте равно
        // единице, и после переходного процесса выход снова равен единице. Сброс состояния
        // вернул бы выход к нулю, и звук на секунду провалился бы.
        var equalizer = new PeakEqualizer(SampleRate, 1, [new EqualizerBand(1000f, 6f)]);

        var steady = new float[SampleRate];
        Array.Fill(steady, 1f);
        equalizer.Apply(steady);

        Assert.InRange(steady[^1], 0.95f, 1.05f);

        equalizer.UpdateBands([new EqualizerBand(1000f, 6f)]);

        var tail = new float[100];
        Array.Fill(tail, 1f);
        equalizer.Apply(tail);

        Assert.InRange(tail[0], 0.95f, 1.05f);
    }

    [Fact]
    public void ДвухканальныйСигнал_ФильтруетсяПоКаналам()
    {
        // Слева синус 1 кГц, справа - 5 кГц. Полоса поднята на 1 кГц: левый канал вырастает,
        // правый остаётся прежним. Если бы каналы фильтровались вперемешку, правый
        // канал получил бы кусок чужой полосы.
        var input = TestTone.Stereo(SampleRate, 1000f, 5000f, 0.5f, SampleRate);
        var equalizer = new PeakEqualizer(SampleRate, 2, [new EqualizerBand(1000f, 6f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        var leftRatio = TestTone.Rms(TestTone.Channel(output, 0), SampleRate / 2)
            / TestTone.Rms(TestTone.Channel(input, 0), SampleRate / 2);
        var rightRatio = TestTone.Rms(TestTone.Channel(output, 1), SampleRate / 2)
            / TestTone.Rms(TestTone.Channel(input, 1), SampleRate / 2);

        Assert.InRange(leftRatio, 1.8d, 2.2d);
        Assert.InRange(rightRatio, 0.95d, 1.05d);
    }

    [Fact]
    public void ОдноканальныйСигналНеСмещается()
    {
        // Один канал: сэмплы идут подряд, и каждый второй не должен попадать в «чужой» канал.
        var input = TestTone.Sine(SampleRate, 1000f, 0.5f, SampleRate);
        var equalizer = new PeakEqualizer(SampleRate, 1, [new EqualizerBand(1000f, 6f)]);

        var output = (float[])input.Clone();
        equalizer.Apply(output);

        var ratio = TestTone.Rms(output, SampleRate / 2) / TestTone.Rms(input, SampleRate / 2);

        Assert.InRange(ratio, 1.8d, 2.2d);
    }
}
