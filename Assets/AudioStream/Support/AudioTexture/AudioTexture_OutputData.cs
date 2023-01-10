// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using UnityEngine;

namespace AudioStreamSupport
{
    public class AudioTexture_OutputData : AudioTexture_Base
    {
        // output data window : power of two max 16384
        public enum OUTPUTDATA_WINDOW_SIZE
        {
            TWOTO1_2 = 2
            , TWOTO2_4 = 4
            , TWOTO3_8 = 8
            , TWOTO4_16 = 16
            , TWOTO5_32 = 32
            , TWOTO6_64 = 64
            , TWOTO7_128 = 128
            , TWOTO8_256 = 256
            , TWOTO9_512 = 512
            , TWOTO10_1024 = 1024
            , TWOTO11_2048 = 2048
            , TWOTO12_4096 = 4096
            , TWOTO13_8192 = 8192
            , TWOTO14_16384 = 16384
        }
        public OUTPUTDATA_WINDOW_SIZE windowSize = OUTPUTDATA_WINDOW_SIZE.TWOTO10_1024;

        float[] samplesOfChannel;
        float[] samplesAvg;

        protected override void Awake()
        {
            base.Awake();

            this.samplesOfChannel = new float[(int)this.windowSize];
            this.samplesAvg = new float[(int)this.windowSize];

            this.outputTexture = new Texture2D((int)this.windowSize, this.outputTextureHeight, TextureFormat.ARGB32, false);
        }
        void Update()
        {
            if (this.samplesOfChannel.Length != (int)this.windowSize
                || this.outputTexture.height != this.outputTextureHeight
                )
            {
                this.samplesOfChannel = new float[(int)this.windowSize];
                this.samplesAvg = new float[(int)this.windowSize];
                this.outputTexture = new Texture2D((int)this.windowSize, this.outputTextureHeight, TextureFormat.ARGB32, false);
            }

            for (var ch = 0; ch < this.channels; ++ch)
            {
                // [-1, 1]
                if (this.@as)
                    this.@as.GetOutputData(this.samplesOfChannel, ch);
                else
                    AudioListener.GetOutputData(this.samplesOfChannel, ch);

                for (var i = 0; i < (int)this.windowSize; ++i)
                    this.samplesAvg[i] += this.samplesOfChannel[i];
            }

            for (var i = 0; i < (int)this.windowSize; ++i)
                this.samplesAvg[i] /= this.channels;

            for (var x = 0; x < (int)this.windowSize; ++x)
            {
                // [-1, 1] => [-100, 100] => [0, 200] => [0, 100]
                var sample_in_texture = (int)((this.samplesAvg[x] * this.outputTextureHeight) + this.outputTextureHeight / 2f);

                for (var y = 0; y < this.outputTextureHeight; ++y)
                {
                    if (y == sample_in_texture)
                        this.outputTexture.SetPixel(x, y, this.outputTextureValueColor);
                    else
                        this.outputTexture.SetPixel(x, y, this.outputTextureBackgroundColor);
                }
            }

            this.outputTexture.Apply();
        }
    }
}