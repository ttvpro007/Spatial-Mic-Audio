// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using UnityEngine;

namespace AudioStreamSupport
{
    public class AudioTexture_Base : MonoBehaviour
    {
        /// <summary>
        /// User AudioSource
        /// </summary>
        [SerializeField]
        private AudioSource audioSource = null;

        protected AudioSource @as;
        protected float channels;

        public Texture2D outputTexture;
        [Range(32, 512)]
        public int outputTextureHeight = 100;

        public Color outputTextureValueColor = Color.white;
        public Color outputTextureBackgroundColor = Color.clear;

        protected virtual void Awake()
        {
            // use either referenced/user or this' AudioSource
            if (this.audioSource)
                this.@as = this.audioSource;
            else
                this.@as = this.GetComponent<AudioSource>();

            this.channels = AudioStreamSupport.UnityAudio.ChannelsFromUnityDefaultSpeakerMode();
        }
    }
}