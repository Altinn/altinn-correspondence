using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class StringExtensionsTests
    {
        [Fact]
        public void IsSocialSecurityNumber_ReturnsFalse_IfIdentifierNotValidSSN()
        {
            // Arrange
            string socialSecurityNumber = "01234567890"; //Syntethic invalid social security number

            // Act
            bool isValid = StringExtensions.IsSocialSecurityNumber(socialSecurityNumber);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void IsSocialSecurityNumber_ReturnsTrue_IfIdentifierValidSSN()
        {
            // Arrange
            string socialSecurityNumber = "08900499559"; //Syntethic valid social security number

            // Act
            bool isValid = StringExtensions.IsSocialSecurityNumber(socialSecurityNumber);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void IsSocialSecurityNumber_ReturnsTrue_IfIdentifierValidSSNWithPrefix()
        {
            // Arrange
            string socialSecurityNumber = "urn:altinn:person:identifier-no:08900499559"; //Syntethic valid social security number

            // Act
            bool isValid = StringExtensions.IsSocialSecurityNumber(socialSecurityNumber);

            // Assert
            Assert.True(isValid);
        }
    }
}