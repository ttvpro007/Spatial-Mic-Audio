using UnityEngine;
using AudioStream;

[RequireComponent(typeof(ResonanceInput))]
public class ResonanceMicrophoneSelector : MonoBehaviour
{
    public int Id => id;

    [SerializeField]
    private string microphone;
    [SerializeField, AudioStreamSupport.ReadOnly]
    private int id = 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        GetComponent<ResonanceInput>().recordDeviceId = id;
    }
#endif
}
