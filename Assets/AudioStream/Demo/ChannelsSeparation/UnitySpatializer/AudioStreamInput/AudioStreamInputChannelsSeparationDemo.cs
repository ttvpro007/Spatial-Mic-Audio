// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using AudioStream;
using System.Collections;
using UnityEngine;
/// <summary>
/// Waits until recording component instantiates AudioSource prefabs per input channel and places them evenly in front of the camera/listener
/// </summary>
[ExecuteInEditMode()]
public class AudioStreamInputChannelsSeparationDemo : MonoBehaviour
{
    public AudioStreamInputChannelsSeparation audioStreamInputChannelsSeparation;
    public AudioListener listener;

    IEnumerator Start()
    {
        // w8 until channels are created and spread them evenly on arc in front of listener/camera
        while (this.audioStreamInputChannelsSeparation.audioSourceChannels == null
            || this.audioStreamInputChannelsSeparation.audioSourceChannels.Length < 1)
            yield return null;

        // from left to right
        var a = Mathf.PI;
        var radius = 25f;
        var step = Mathf.PI / (float)(this.audioStreamInputChannelsSeparation.audioSourceChannels.Length - 1);

        for (var i = 0; i < this.audioStreamInputChannelsSeparation.audioSourceChannels.Length; ++i)
        {
            var asc = this.audioStreamInputChannelsSeparation.audioSourceChannels[i];
            asc.transform.position = new Vector3(Mathf.Cos(a) * radius
                , 0
                , Mathf.Sin(a) * radius
                );
            a -= step;
        }
    }

    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioStreamInputChannelsSeparation != null ? " " + this.audioStreamInputChannelsSeparation.fmodVersion : "");

        GUILayout.Label("This scene starts recording from system default (0) input device and instantiates single channel AudioSource per recording channel in the scene with audio from it\r\n" +
            "(it records just from default input to keep the scene simple - please see one of the AudioStreamInput* demo scenes to see how to enumerate and record from all input devices, or change your system default intput device before running)");
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");
    }
}
