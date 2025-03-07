using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class StringExtensionsTests
    {
        [Fact]
        public void IsValidSocialSecurityNumber_ReturnsFalse_IfIdentifierNotValidSSN()
        {
            // Arrange
            string socialSecurityNumber = "01234567890"; //Syntethic invalid social security number

            // Act
            bool isValid = StringExtensions.IsValidSocialSecurityNumber(socialSecurityNumber);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void IsValidSocialSecurityNumber_ReturnsTrue_IfIdentifierValidSSN()
        {
            // Arrange
            string socialSecurityNumber = "08900499559"; //Syntethic valid social security number

            // Act
            bool isValid = StringExtensions.IsValidSocialSecurityNumber(socialSecurityNumber);

            // Assert
            Assert.True(isValid);
        }
    }
}