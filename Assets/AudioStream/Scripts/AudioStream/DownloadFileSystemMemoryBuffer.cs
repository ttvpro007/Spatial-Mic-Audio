// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements file system for FMOD via memory buffer which get discarded as playback/FMOD reads progress
    /// </summary>
    public class DownloadFileSystemMemoryBuffer : DownloadFileSystemBase
    {
        /// <summary>
        /// backing store
        /// </summary>
        List<byte> buffer = new List<byte>();
        /// <summary>
        /// to offset reads after discarding previously read data
        /// </summary>
        uint bytesRemoved;
        public override uint capacity
        {
            get
            {
                return (uint)(this.buffer.Count);
            }

            protected set
            {
                throw new NotImplementedException();
            }
        }
        public override uint available { get; protected set; }

        public DownloadFileSystemMemoryBuffer(uint _decoder_block_size)
            :base(_decoder_block_size)
        {
        }

        public override void Write(byte[] bytes)
        {
            lock (this.bufferLock)
            {
                this.buffer.AddRange(bytes);
            }
        }
        
        public override byte[] Read(uint offset, uint toread)
        {
            // initial seek/s
            if (offset > uint.MaxValue - 100000)
                return new byte[0];

            // adjust shift the offset based on how much was discarded so far
            offset -= this.bytesRemoved;

            var av = this.buffer.Count - offset;
            if (av < 1)
            {
                Debug.LogFormat("Read underflow, offset: {0} length: {1}, available: {2}", offset, toread, av);
                return new byte[0];
            }

            this.available = (uint)av;

            var result_size = (uint)Mathf.Min(this.available, toread);
            var result = new byte[result_size];

            lock (this.bufferLock)
                Array.Copy(this.buffer.ToArray(), offset, result, 0, result_size);

            // if 'enough' was read, discard previously played data in the buffer
            // allocated == (offset + toread) + whatever was downloaded up to that point
            // ~ 10MB + dl
            // should hopefully cover all cases such as album artwork etc.
            // TODO: make this size configurable
            if (offset + toread >= 10485760)
            {
                lock (this.bufferLock)
                    this.buffer.RemoveRange(0, (int)toread);

                this.bytesRemoved += toread;

                // Debug.LogFormat("Discarded {0} @ R:{1}", toread, offset);
            }

            return result;
        }

        public override void CloseStore()
        {
            lock (this.bufferLock)
            {
                this.buffer.Clear();
                this.buffer = null;
            }
        }
    }
}