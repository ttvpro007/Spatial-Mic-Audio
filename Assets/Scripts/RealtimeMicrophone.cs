using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RealtimeMicrophone : MonoBehaviour
{
    [SerializeField]
    private string microphone = string.Empty;

    public string MicrophoneName => microphone;
    public AudioClip MicrophoneClip => microphoneClip;
    
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        SetMic();
    }

    private void SetMic()
    {
        microphoneClip = Microphone.Start(microphone, true, 20, AudioSettings.outputSampleRate);
        audioSource.clip = microphoneClip;
        lastMicrophone = microphone;
    }

    private void OnValidate()
    {
        if (microphone == lastMicrophone || !Application.isPlaying || !audioSource) return;

        if (Microphone.IsRecording(lastMicrophone))
            Microphone.End(lastMicrophone);

        SetMic();
    }

    private string lastMicrophone;
    private AudioSource audioSource;
    private AudioClip microphoneClip;
}