// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Class holding list of PCM samples per specified channel count
    /// Supports thread safe adding and removing samples per channel
    /// </summary>
    public class MultiChannelBuffer
    {
        readonly List<List<float>> channels;
        public int channelCount { get { return this.channels.Count; } }
        object @lock = new object();

        public MultiChannelBuffer(int ofChannels)
        {
            this.channels = new List<List<float>>();

            for (var i = 0; i < ofChannels; ++i)
                this.channels.Add(new List<float>());
        }

        public void Add(int toChannel, float[] pcm)
        {
            lock (this.@lock)
                this.channels[toChannel].AddRange(pcm);
        }

        public float[] Remove(int fromChannel, int count)
        {
            lock(this.@lock)
            {
                var c = Mathf.Min(count, this.channels[fromChannel].Count);
                var arr = this.channels[fromChannel].GetRange(0, c).ToArray();
                this.channels[fromChannel].RemoveRange(0, c);
                return arr;
            }
        }

        public int SampleCount(int ofChannel)
        {
            lock (this.@lock)
                return this.channels[ofChannel].Count;
        }
    }
}