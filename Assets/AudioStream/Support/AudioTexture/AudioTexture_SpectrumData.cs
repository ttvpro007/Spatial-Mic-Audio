// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using UnityEngine;

namespace AudioStreamSupport
{
    public class AudioTexture_SpectrumData : AudioTexture_Base
    {
        // spectrum data: power of two between 64 and 8192
        public enum SPECTRUMDATA_WINDOW_SIZE
        {
            TWOTO6_64 = 64
            , TWOTO7_128 = 128
            , TWOTO8_256 = 256
            , TWOTO9_512 = 512
            , TWOTO10_1024 = 1024
            , TWOTO11_2048 = 2048
            , TWOTO12_4096 = 4096
            , TWOTO13_8192 = 8192
        }
        public SPECTRUMDATA_WINDOW_SIZE windowSize = SPECTRUMDATA_WINDOW_SIZE.TWOTO8_256;
        public FFTWindow fftWindow = FFTWindow.Rectangular;

        protected float[] spectrumOfChannel;
        protected float[] spectrumAvg;

        protected override void Awake()
        {
            base.Awake();

            this.spectrumOfChannel = new float[(int)this.windowSize];
            this.spectrumAvg = new float[(int)this.windowSize];

            this.outputTexture = new Texture2D((int)this.windowSize, this.outputTextureHeight, TextureFormat.ARGB32, false);
        }

        void Update()
        {
            if (this.spectrumOfChannel.Length != (int)this.windowSize
                || this.outputTexture.height != this.outputTextureHeight
                )
            {
                this.spectrumOfChannel = new float[(int)this.windowSize];
                this.spectrumAvg = new float[(int)this.windowSize];
                this.outputTexture = new Texture2D((int)this.windowSize, this.outputTextureHeight, TextureFormat.ARGB32, false);
            }

            for (var ch = 0; ch < this.channels; ++ch)
            {
                // [0, 1]
                if (this.@as)
                    this.@as.GetSpectrumData(this.spectrumOfChannel, ch, this.fftWindow);
                else
                    AudioListener.GetSpectrumData(this.spectrumOfChannel, ch, this.fftWindow);

                for (var i = 0; i < (int)this.windowSize; ++i)
                    this.spectrumAvg[i] += this.spectrumOfChannel[i];
            }

            for (var i = 0; i < (int)this.windowSize; ++i)
                this.spectrumAvg[i] /= this.channels;

            for (var x = 0; x < (int)this.windowSize; ++x)
            {
                // [0, 1] => [0, 100]
                // plus spectrum(x) ∘ ln(x) with 1 @ 0 to augment higher bands
                var lnscale = 1f + Mathf.Log(x + 1f);
                var spectrum_in_texture = (int)(this.spectrumAvg[x] * this.outputTextureHeight * lnscale);

                for (var y = 0; y < this.outputTextureHeight; ++y)
                {
                    if (y == spectrum_in_texture)
                        this.outputTexture.SetPixel(x, y, this.outputTextureValueColor);
                    else
                        this.outputTexture.SetPixel(x, y, this.outputTextureBackgroundColor);
                }
            }

            this.outputTexture.Apply();
        }
    }
}