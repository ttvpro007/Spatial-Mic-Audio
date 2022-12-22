using UnityEngine;

public static class AudioSpectrumSampling
{
    private static int SAMPLE_SIZE = 64;
    private static float maxValue = 1f;

    public static float GetAverageAmplitude(int clipPosition, AudioClip clip)
    {
        int start = Mathf.Max(0, clipPosition - SAMPLE_SIZE);
        float[] waveData = new float[SAMPLE_SIZE];

        if (clip && clip.GetData(waveData, start))
        {
            float spectrum = 0f;

            for (int i = 0; i < SAMPLE_SIZE; i++)
            {
                spectrum += Mathf.Abs(waveData[i]);
            }

            spectrum /= SAMPLE_SIZE;

            if (maxValue < spectrum) maxValue = spectrum;

            return spectrum / maxValue;
        }

        return 0f;
    }
}
