using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class GuidExtensionsTests
    {
        [Fact]
        public void CreateVersion5_IsDeterministic_ForSameInputs()
        {
            // Arrange
            var ns = Guid.NewGuid();
            var name = "recipient:org:000111222";

            // Act
            var g1 = ns.CreateVersion5(name);
            var g2 = ns.CreateVersion5(name);

            // Assert
            Assert.Equal(g1, g2);
        }

        [Fact]
        public void CreateVersion5_DiffNames_ProduceDifferentGuids()
        {
            // Arrange
            var ns = Guid.NewGuid();

            // Act
            var g1 = ns.CreateVersion5("name-one");
            var g2 = ns.CreateVersion5("name-two");

            // Assert
            Assert.NotEqual(g1, g2);
        }

        [Fact]
        public void CreateVersion5_DiffNamespaces_ProduceDifferentGuids()
        {
            // Arrange
            var ns1 = Guid.NewGuid();
            var ns2 = Guid.NewGuid();
            var name = "same";

            // Act
            var g1 = ns1.CreateVersion5(name);
            var g2 = ns2.CreateVersion5(name);

            // Assert
            Assert.NotEqual(g1, g2);
        }

        [Fact]
        public void CreateVersion5_SetsVersion5_And_Rfc4122Variant()
        {
            // Arrange
            var ns = Guid.NewGuid();
            var name = "any";

            // Act
            var g = ns.CreateVersion5(name);
            var bytes = g.ToByteArray();

            // Guid byte order in .NET is little-endian for time fields
            // Extract version from bytes[7:6] nibble after swapping
            var be = (byte[])bytes.Clone();
            Array.Reverse(be, 0, 4);
            Array.Reverse(be, 4, 2);
            Array.Reverse(be, 6, 2);

            int version = (be[6] >> 4) & 0x0F;
            int variant = (be[8] >> 6) & 0x03;

            // Assert
            Assert.Equal(5, version);
            // RFC 4122 variant is 0b10
            Assert.Equal(2, variant);
            // Also validate the exact top-2 bits mask
            Assert.True((be[8] & 0xC0) == 0x80);
        }
    }
}


