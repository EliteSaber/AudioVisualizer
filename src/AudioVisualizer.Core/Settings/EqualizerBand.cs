namespace AudioVisualizer.Core.Settings;

/// <summary>Полоса визуального эквалайзера: усиление в децибелах на центральной частоте.</summary>
public sealed record EqualizerBand(float CenterFrequencyHz, float GainDb);