using AudioVisualizer.Core.Settings;
using NAudio.Wave;

namespace AudioVisualizer.Core.Audio;

/// <summary>
/// Открытый аудиотрек. Наследуется от <see cref="IWaveProvider"/>, потому что так устроен
/// <c>AudioFileReader</c> и адаптер поверх него остаётся тонким.
/// </summary>
/// <remarks>
/// Интерфейс нужен, чтобы состояние плеера проверялось тестами: настоящий вывод требует
/// звуковой карты, а на нём держатся перемотка, пауза и возобновление с места паузы.
/// </remarks>
public interface IAudioTrack : IWaveProvider, IDisposable
{
    TimeSpan Position { get; set; }

    TimeSpan Duration { get; }

    float Volume { get; set; }

    /// <summary>
    /// Задаёт полосы эквалайзера для звука при воспроизведении.
    /// </summary>
    /// <param name="bands">Полосы: центральная частота и усиление.</param>
    /// <param name="enabled">Включён ли эквалайзер. Выключенный звук идёт без обработки.</param>
    void SetEqualizer(IReadOnlyList<EqualizerBand> bands, bool enabled);
}
