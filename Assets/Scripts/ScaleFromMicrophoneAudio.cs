using UnityEngine;

[RequireComponent(typeof(RealtimeMicrophone))]
public class ScaleFromMicrophoneAudio : MonoBehaviour
{
    [SerializeField]
    private Transform target;
    [SerializeField]
    private Vector3 minScale;
    [SerializeField]
    private Vector3 maxScale;
    [SerializeField]
    private float threshold = 0.01f;

    private RealtimeMicrophone realtimeMicrophone;

    private void Start()
    {
        realtimeMicrophone = GetComponent<RealtimeMicrophone>();
    }

    private void Update()
    {
        if (!target) return;

        float spectrum = AudioSpectrumSampling.GetAverageAmplitude(Microphone.GetPosition(realtimeMicrophone.MicrophoneName), realtimeMicrophone.MicrophoneClip);

        if (spectrum < threshold)
            spectrum = 0f;

        target.localScale = Vector3.Lerp(minScale, maxScale, spectrum);
    }
}
