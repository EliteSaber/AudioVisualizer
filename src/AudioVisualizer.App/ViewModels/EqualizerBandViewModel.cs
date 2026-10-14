using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioVisualizer.App.ViewModels;

/// <summary>Полоса визуального эквалайзера в интерфейсе: частота только показывается, усиление меняется.</summary>
public sealed partial class EqualizerBandViewModel(Action onGainChanged) : ObservableObject
{
    [ObservableProperty]
    private float _gainDb;

    public EqualizerBandViewModel(float centerFrequencyHz, float gainDb, Action onGainChanged)
        : this(onGainChanged)
    {
        CenterFrequencyHz = centerFrequencyHz;
        _gainDb = gainDb;
    }

    public float CenterFrequencyHz { get; }

    public string Title => FormatFrequency(CenterFrequencyHz);

    public void SetGain(float gainDb) => GainDb = gainDb;

    partial void OnGainDbChanged(float value) => onGainChanged();

    private static string FormatFrequency(float frequencyHz) => frequencyHz switch
    {
        >= 1000f => $"{frequencyHz / 1000f:0.#} кГц",
        _ => $"{frequencyHz:0.#} Гц"
    };
}