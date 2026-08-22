using System;
using System.Linq;
using NAudio.Wave;
using Xunit;

namespace SoundBoard.Tests
{
    public class LoopStreamTests
    {
        #region Fake source stream

        /// <summary>
        /// An in-memory <see cref="WaveStream"/> over a byte array. Reads return at most <see cref="MaxBytesPerRead"/> bytes
        /// so that partial reads can be simulated, and 0 at the end like a real file reader.
        /// </summary>
        private sealed class MemoryWaveStream : WaveStream
        {
            private readonly byte[] _data;
            private long _position;

            public MemoryWaveStream(byte[] data) => _data = data;

            public int MaxBytesPerRead { get; set; } = int.MaxValue;
            public int ReadCalls { get; private set; }
            public Func<Exception> ThrowOnRead { get; set; }

            public override WaveFormat WaveFormat { get; } = new WaveFormat(8000, 8, 1);
            public override long Length => _data.Length;

            public override long Position
            {
                get => _position;
                set => _position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadCalls++;
                Exception toThrow = ThrowOnRead?.Invoke();
                if (toThrow != null) throw toThrow;

                int n = (int)Math.Min(Math.Min(count, MaxBytesPerRead), _data.Length - _position);
                Array.Copy(_data, _position, buffer, offset, n);
                _position += n;
                return n;
            }
        }

        private static byte[] Sequence(int length) => Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

        private static byte[] ReadAll(WaveStream stream, int count, int offset = 0)
        {
            var buffer = new byte[count + offset];
            int read = stream.Read(buffer, offset, count);
            return buffer.Skip(offset).Take(read).ToArray();
        }

        #endregion

        [Fact]
        public void Looping_WrapsAroundToFillTheBuffer()
        {
            var source = new MemoryWaveStream(Sequence(10));
            var loop = new LoopStream(source);

            byte[] result = ReadAll(loop, 25);

            Assert.Equal(Sequence(10).Concat(Sequence(10)).Concat(Sequence(5)).ToArray(), result);
            Assert.Equal(5, loop.Position);
        }

        [Fact]
        public void Looping_ContinuesAcrossReads()
        {
            var source = new MemoryWaveStream(Sequence(10));
            var loop = new LoopStream(source);

            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, ReadAll(loop, 8));
            Assert.Equal(new byte[] { 8, 9, 0, 1, 2, 3, 4, 5 }, ReadAll(loop, 8));
            Assert.Equal(new byte[] { 6, 7, 8, 9, 0, 1, 2, 3 }, ReadAll(loop, 8));
        }

        [Fact]
        public void Read_HonorsOffset()
        {
            var loop = new LoopStream(new MemoryWaveStream(Sequence(4)));

            var buffer = new byte[10];
            Assert.Equal(6, loop.Read(buffer, 3, 6));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 1, 2, 3, 0, 1, 0 }, buffer);
        }

        [Fact]
        public void PartialSourceReads_AreAccumulated()
        {
            var source = new MemoryWaveStream(Sequence(10)) { MaxBytesPerRead = 3 };
            var loop = new LoopStream(source);

            Assert.Equal(Sequence(10).Concat(Sequence(4)).ToArray(), ReadAll(loop, 14));
        }

        [Fact]
        public void LoopingDisabled_StopsAtEndOfSource()
        {
            var source = new MemoryWaveStream(Sequence(10));
            var loop = new LoopStream(source) { EnableLooping = false };

            Assert.Equal(Sequence(10), ReadAll(loop, 25));
            Assert.Equal(10, loop.Position);
            Assert.Empty(ReadAll(loop, 25)); // and stays there
        }

        [Fact]
        public void LoopingCanBeTurnedOffMidway()
        {
            var source = new MemoryWaveStream(Sequence(10));
            var loop = new LoopStream(source);

            Assert.Equal(7, ReadAll(loop, 7).Length);
            loop.EnableLooping = false;
            Assert.Equal(new byte[] { 7, 8, 9 }, ReadAll(loop, 7));
            Assert.Empty(ReadAll(loop, 7));
        }

        [Fact]
        public void EmptySource_ReturnsZeroInsteadOfSpinningForever()
        {
            var source = new MemoryWaveStream(new byte[0]);
            var loop = new LoopStream(source);

            Assert.Empty(ReadAll(loop, 16));
            Assert.Equal(1, source.ReadCalls);
        }

        [Fact]
        public void SourceThatStaysAtPositionZero_DoesNotLoopInfinitely()
        {
            // A source whose Read returns 0 without advancing would otherwise be reset to 0 and read again forever
            var source = new MemoryWaveStream(Sequence(10)) { MaxBytesPerRead = 0 };
            var loop = new LoopStream(source);

            Assert.Empty(ReadAll(loop, 16));
        }

        [Fact]
        public void SourceException_IsSwallowedAndReturnsBytesReadSoFar()
        {
            var source = new MemoryWaveStream(Sequence(10)) { MaxBytesPerRead = 4 };
            var loop = new LoopStream(source);
            int calls = 0;
            source.ThrowOnRead = () => ++calls == 2 ? new ObjectDisposedException("source") : null;

            // First read returns 4 bytes, second throws
            byte[] result = ReadAll(loop, 10);

            Assert.Equal(new byte[] { 0, 1, 2, 3 }, result);
        }

        [Fact]
        public void ExceptionOnFirstRead_ReturnsZero()
        {
            var source = new MemoryWaveStream(Sequence(10)) { ThrowOnRead = () => new InvalidOperationException() };

            Assert.Equal(0, new LoopStream(source).Read(new byte[10], 0, 10));
        }

        [Fact]
        public void ZeroCount_ReturnsZeroWithoutTouchingSource()
        {
            var source = new MemoryWaveStream(Sequence(10));

            Assert.Equal(0, new LoopStream(source).Read(new byte[10], 0, 0));
            Assert.Equal(0, source.ReadCalls);
        }

        [Fact]
        public void FormatLengthAndPosition_PassThroughToSource()
        {
            var source = new MemoryWaveStream(Sequence(10));
            var loop = new LoopStream(source);

            Assert.Same(source.WaveFormat, loop.WaveFormat);
            Assert.Equal(10, loop.Length);
            Assert.True(loop.EnableLooping);

            loop.Position = 6;
            Assert.Equal(6, source.Position);
            Assert.Equal(new byte[] { 6, 7, 8, 9, 0, 1 }, ReadAll(loop, 6));
        }
    }
}
