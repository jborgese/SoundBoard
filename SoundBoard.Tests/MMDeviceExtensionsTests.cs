using System;
using Xunit;

namespace SoundBoard.Tests
{
    public class MMDeviceExtensionsTests
    {
        [Theory]
        [InlineData("{0.0.0.00000000}.{a1b2c3d4-0000-0000-0000-000000000001}", "a1b2c3d4-0000-0000-0000-000000000001")]
        [InlineData("{0.0.1.00000000}.{B1B2C3D4-0000-0000-0000-000000000003}", "b1b2c3d4-0000-0000-0000-000000000003")]
        [InlineData("{0.0.0.00000000}.{a1b2c3d4-0000-0000-0000-000000000001}trailing", "a1b2c3d4-0000-0000-0000-000000000001")]
        public void RealShapedIds_Parse(string id, string expected)
        {
            Assert.True(MMDeviceExtensions.TryParseDeviceId(id, out Guid guid));
            Assert.Equal(Guid.Parse(expected), guid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("no braces at all")]
        [InlineData("{0.0.0.00000000}.{a1b2c3d4-0000-0000-0000-00000000000}")]   // guid one char short
        [InlineData("{0.0.0.00000000}.{not-a-guid-at-all-xxxxxxxxxxxxxxxxx}")]    // right length, wrong content
        [InlineData("{a1b2c3d4-0000-0000-0000-000000000001}")]                   // only one brace group: the search starts at index 1
        public void UnparseableIds_ReturnFalseAndEmptyGuid_WithoutThrowing(string id)
        {
            Assert.False(MMDeviceExtensions.TryParseDeviceId(id, out Guid guid));
            Assert.Equal(Guid.Empty, guid);
        }

        [Fact]
        public void SecondBraceGroupIsUsed_EvenIfFirstLooksLikeAGuid()
        {
            const string first = "11111111-1111-1111-1111-111111111111";
            const string second = "22222222-2222-2222-2222-222222222222";

            Assert.True(MMDeviceExtensions.TryParseDeviceId("{" + first + "}.{" + second + "}", out Guid guid));
            Assert.Equal(Guid.Parse(second), guid);
        }

        [Fact]
        public void EmptyGuidInId_IsParsedAsEmpty_NotFailure()
        {
            // Distinguishing "parse failed" from "the default device" is what callers rely on; a literal empty guid is a success.
            Assert.True(MMDeviceExtensions.TryParseDeviceId("{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}", out Guid guid));
            Assert.Equal(Guid.Empty, guid);
        }
    }
}
