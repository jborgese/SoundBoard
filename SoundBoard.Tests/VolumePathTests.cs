using System;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Xunit;

namespace SoundBoard.Tests
{
    /// <summary>
    /// <c>SoundPlayer</c> puts a <see cref="VolumeSampleProvider"/> in front of every output, including at a volume
    /// offset of zero with nothing muted, because mute has to be able to take hold part-way through a sound and
    /// <c>DirectSoundOut</c> refuses to have its own <c>Volume</c> set.
    /// </summary>
    /// <remarks>
    /// These pin the two things that have to hold for that to be free rather than a change to how everything sounds:
    /// the device must be handed the same format it was handed before, and at full volume not one sample may differ.
    /// They assert on the NAudio composition rather than on <c>SoundPlayer</c> itself because the rest of that class
    /// cannot run without a real output device — but the composition is the part the claim rests on, and an NAudio
    /// upgrade that quietly changed it would change what every user hears.
    /// </remarks>
    public class VolumePathTests
    {
        #region Fake source stream

        /// <summary>
        /// An in-memory 32-bit IEEE float stream. That is deliberately the same format <c>AudioFileReader</c> hands
        /// out whatever the file on disk was — it decodes everything to float — so this is what the old code path
        /// gave the device directly.
        /// </summary>
        private sealed class FloatWaveStream : WaveStream
        {
            private readonly byte[] _data;
            private long _position;

            public FloatWaveStream(float[] samples)
            {
                _data = new byte[samples.Length * sizeof(float)];
                Buffer.BlockCopy(samples, 0, _data, 0, _data.Length);
            }

            public override WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

            public override long Length => _data.Length;

            public override long Position
            {
                get => _position;
                set => _position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = (int)Math.Min(count, _data.Length - _position);
                Array.Copy(_data, _position, buffer, offset, n);
                _position += n;
                return n;
            }
        }

        /// <summary>
        /// A spread of awkward values: silence, both full-scale rails, something past them, and a few ordinary samples.
        /// </summary>
        private static float[] Samples() => new[]
        {
            0f, 1f, -1f, 0.5f, -0.5f, 0.123456f, -0.987654f, 1.5f, -1.5f, float.Epsilon
        };

        private static byte[] ReadAll(IWaveProvider provider)
        {
            var buffer = new byte[64];
            var all = Enumerable.Empty<byte>();
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                all = all.Concat(buffer.Take(read).ToArray());
            }
            return all.ToArray();
        }

        /// <summary>
        /// The chain SoundPlayer builds. <c>IWavePlayer.Init(ISampleProvider)</c> wraps the sample provider in a
        /// <see cref="SampleToWaveProvider"/> before handing it to the device, so that is what the device really sees.
        /// </summary>
        private static IWaveProvider WrappedLikeSoundPlayer(WaveStream source, float volume) =>
            new SampleToWaveProvider(new VolumeSampleProvider(source.ToSampleProvider()) { Volume = volume });

        #endregion

        [Fact]
        public void WrappingInVolume_HandsTheDeviceTheSameFormat()
        {
            var direct = new FloatWaveStream(Samples());
            var wrapped = WrappedLikeSoundPlayer(new FloatWaveStream(Samples()), 1f);

            Assert.Equal(direct.WaveFormat.Encoding, wrapped.WaveFormat.Encoding);
            Assert.Equal(direct.WaveFormat.SampleRate, wrapped.WaveFormat.SampleRate);
            Assert.Equal(direct.WaveFormat.Channels, wrapped.WaveFormat.Channels);
            Assert.Equal(direct.WaveFormat.BitsPerSample, wrapped.WaveFormat.BitsPerSample);
        }

        [Fact]
        public void AtFullVolume_NotOneByteDiffers()
        {
            byte[] direct = ReadAll(new FloatWaveStream(Samples()));
            byte[] wrapped = ReadAll(WrappedLikeSoundPlayer(new FloatWaveStream(Samples()), 1f));

            // Byte-for-byte, not approximately: a multiply by 1.0f is exact in IEEE 754, so going through the volume
            // provider at full volume has to be bit-transparent. If this ever fails, every sound is being altered.
            Assert.Equal(direct, wrapped);
        }

        [Fact]
        public void AtZeroVolume_EverySampleIsSilence()
        {
            byte[] muted = ReadAll(WrappedLikeSoundPlayer(new FloatWaveStream(Samples()), 0f));

            // The same length as the source, but silent - muting must not shorten or stall the stream, or the sound
            // would stop rather than go quiet and the progress bar would never reach the end.
            Assert.Equal(Samples().Length * sizeof(float), muted.Length);

            // Compared as samples, not as bytes. A negative sample times zero volume is negative zero, whose bit
            // pattern is 0x80000000 rather than all zeroes, so a byte-wise check reports four "failures" on a buffer
            // that is in fact perfectly silent: -0.0f == 0.0f, and no converter or DAC can tell them apart.
            var samples = new float[muted.Length / sizeof(float)];
            Buffer.BlockCopy(muted, 0, samples, 0, muted.Length);

            Assert.All(samples, sample => Assert.Equal(0f, sample));
        }

        [Fact]
        public void LoopingIsUnaffectedByTheVolumeWrapper()
        {
            // Looping composes with volume the same way: SoundPlayer wraps the LoopStream, not the reader.
            var looped = new LoopStream(new FloatWaveStream(Samples()));
            IWaveProvider wrapped = new SampleToWaveProvider(new VolumeSampleProvider(looped.ToSampleProvider()));

            Assert.Equal(looped.WaveFormat.Encoding, wrapped.WaveFormat.Encoding);
            Assert.Equal(looped.WaveFormat.BitsPerSample, wrapped.WaveFormat.BitsPerSample);

            // A loop never ends, so ask for more than the source holds and expect a full buffer rather than a short read.
            var buffer = new byte[Samples().Length * sizeof(float) * 3];
            Assert.Equal(buffer.Length, wrapped.Read(buffer, 0, buffer.Length));
        }
    }
}
