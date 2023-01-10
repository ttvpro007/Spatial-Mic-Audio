// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using UnityEngine;

namespace AudioStream
{
    [ExecuteInEditMode()]
    public class AudioStreamNetMQClientDemo : MonoBehaviour
    {
        public AudioStreamNetMQClient audioStreamNetMQClient;

        System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);
        Vector2 scrollPosition = Vector2.zero;

        void OnGUI()
        {
            AudioStreamDemoSupport.OnGUI_GUIHeader("");

            this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

            GUILayout.Label("If a socket is left connected from previous scene, press Disconnect first.", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.Label("==== Decoder", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Decoder thread priority: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQClient.decoderThreadPriority = (System.Threading.ThreadPriority)GUILayout.SelectionGrid((int)this.audioStreamNetMQClient.decoderThreadPriority, System.Enum.GetNames(typeof(System.Threading.ThreadPriority)), 5, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
            GUILayout.EndHorizontal();

            if (!this.audioStreamNetMQClient.isConnected)
            {
                GUILayout.Space(10);

                GUILayout.Label("==== Network");

                GUILayout.Label("Enter running AudioStream server IP and port below and press Connect", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server IP: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.serverIP = GUILayout.TextField(this.audioStreamNetMQClient.serverIP, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server port: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.serverTransferPort = int.Parse(GUILayout.TextField(this.audioStreamNetMQClient.serverTransferPort.ToString(), GUILayout.MaxWidth(Screen.width / 4)));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Connect", AudioStreamSupport.UX.guiStyleButtonNormal))
                    this.audioStreamNetMQClient.Connect();
            }
            else
            {
                GUILayout.Label("Decode info:");
                GUILayout.Label(string.Format("Current frame size: {0}", this.audioStreamNetMQClient.opusPacket_frameSize), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Bandwidth: {0}", this.audioStreamNetMQClient.opusPacket_Bandwidth), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Mode: {0}", this.audioStreamNetMQClient.opusPacket_Mode), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Channels: {0}", this.audioStreamNetMQClient.opusPacket_Channels), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Frames per packet: {0}", this.audioStreamNetMQClient.opusPacket_NumFramesPerPacket), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Samples per frame: {0}", this.audioStreamNetMQClient.opusPacket_NumSamplesPerFrame), AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.Space(10);

                GUILayout.Label("==== Audio source");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.volume = GUILayout.HorizontalSlider(this.audioStreamNetMQClient.volume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(Mathf.RoundToInt(this.audioStreamNetMQClient.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("==== Network", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.Label(string.Format("Connected to: {0}:{1}", this.audioStreamNetMQClient.serverIP, this.audioStreamNetMQClient.serverTransferPort), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Codec sample rate: {0}, channels: {1}", AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Server sample rate: {0}, channels: {1}", this.audioStreamNetMQClient.serverSamplerate, this.audioStreamNetMQClient.serverChannels), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Network buffer size: {0}", this.audioStreamNetMQClient.networkQueueSize), AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Client thread sleep timeout: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQClient.clientThreadSleepTimeout = (int)GUILayout.HorizontalSlider(audioStreamNetMQClient.clientThreadSleepTimeout, 1, 20, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(this.audioStreamNetMQClient.clientThreadSleepTimeout.ToString().PadLeft(2, '0') + " ms", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("==== Status", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.Label(string.Format("State = {0} {1}"
                    , this.audioStreamNetMQClient.decoderRunning ? "Playing" : "Stopped"
                    , this.audioStreamNetMQClient.lastErrorString
                    )
                    , AudioStreamSupport.UX.guiStyleLabelNormal
                    );

                GUILayout.BeginHorizontal();

                GUILayout.Label(string.Format("Audio buffer size: {0} / available: {1}", this.audioStreamNetMQClient.dspBufferSize, this.audioStreamNetMQClient.capturedAudioSamples), AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                var r = Mathf.CeilToInt(((float)this.audioStreamNetMQClient.capturedAudioSamples / (float)this.audioStreamNetMQClient.dspBufferSize) * 10f);
                var c = Mathf.Min(r, 10);

                GUI.color = this.audioStreamNetMQClient.capturedAudioFrame ? Color.Lerp(Color.red, Color.green, c / 10f) : Color.red;

                this.gauge.Length = 0;
                for (int i = 0; i < c; ++i) this.gauge.Append("#");
                GUILayout.Label(this.gauge.ToString(), AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUILayout.EndHorizontal();

                GUI.color = Color.white;

                GUILayout.Space(20);
                
                if (GUILayout.Button("Disconnect", AudioStreamSupport.UX.guiStyleButtonNormal))
                    this.audioStreamNetMQClient.Disconnect();
            }

            GUILayout.Space(40);

            GUILayout.EndScrollView();
        }
    }
}