// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using AudioStream;
using System.Collections;
using UnityEngine;
/// <summary>
/// 
/// </summary>
[ExecuteInEditMode()]
public class AudioClipChannelsSeparationDemo : MonoBehaviour
{
    public AudioClipChannelsSeparation audioClipChannelsSeparation;
    public AudioListener listener;

    IEnumerator Start()
    {
        // w8 until channels are created and spread them evenly on arc in front of listener/camera
        while (this.audioClipChannelsSeparation.audioSourceChannels == null
            || this.audioClipChannelsSeparation.audioSourceChannels.Length < 1)
            yield return null;

        // from right to left
        var a = 0f;
        var radius = 70f;
        var step = Mathf.PI / (float)(this.audioClipChannelsSeparation.audioSourceChannels.Length - 1);

        for (var i = 0; i < this.audioClipChannelsSeparation.audioSourceChannels.Length; ++i)
        {
            var asc = this.audioClipChannelsSeparation.audioSourceChannels[i];
            asc.transform.position = new Vector3(Mathf.Cos(a) * radius
                , 0
                , Mathf.Sin(a) * radius
                );
            // enlarge graphics a little
            asc.transform.localScale *= 5;
            a += step;
        }
    }

    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader("");

        GUILayout.Label("This scene plays a multichannel AudioSource/AudioClip and instantiates single channel AudioSource per AudioClip channel in the scene with audio from it\r\n" +
            "- original AudioClip channels are preserved and played individually, AudioClip needs to be processed before playing");
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");
    }
}