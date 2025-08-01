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

        [Fact]
        public void SanitizeForLogging_ReturnsEmpty_WhenInputIsEmpty()
        {
            // Arrange
            string input = "";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void SanitizeForLogging_ReturnsNull_WhenInputIsNull()
        {
            // Arrange
            string? input = null;

            // Act
            string? result = input.SanitizeForLogging();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SanitizeForLogging_EscapesBasicControlCharacters()
        {
            // Arrange
            string input = "Line1\rLine2\nLine3\tTabbed";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("Line1\\rLine2\\nLine3\\tTabbed", result);
        }

        [Fact]
        public void SanitizeForLogging_EscapesAdditionalControlCharacters()
        {
            // Arrange
            string input = "Text\0null\bbackspace\fformfeed\vverticalTab";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("Text\\0null\\bbackspace\\fformfeed\\vverticalTab", result);
        }

        [Fact]
        public void SanitizeForLogging_EscapesHtmlXmlCharacters()
        {
            // Arrange
            string input = "<script>alert('xss')</script>";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;", result);
        }

        [Fact]
        public void SanitizeForLogging_EscapesQuotes()
        {
            // Arrange
            string input = "He said \"Hello\" and she replied 'Hi'";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("He said &quot;Hello&quot; and she replied &#x27;Hi&#x27;", result);
        }

        [Fact]
        public void SanitizeForLogging_EscapesAmpersand()
        {
            // Arrange
            string input = "Tom & Jerry";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("Tom &amp; Jerry", result);
        }

        [Fact]
        public void SanitizeForLogging_HandlesUnicodeControlCharacters()
        {
            // Arrange
            string input = "Text\u0001\u0002\u001FMoreText";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("Text\\u0001\\u0002\\u001FMoreText", result);
        }

        [Fact]
        public void SanitizeForLogging_TruncatesLongStrings()
        {
            // Arrange
            string input = new string('A', 1500); // 1500 characters

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal(1000, result.Length);
            Assert.EndsWith("...", result);
            Assert.Equal(997, result.Length - 3); // 997 chars + "..."
        }

        [Fact]
        public void SanitizeForLogging_PreservesNormalText()
        {
            // Arrange
            string input = "This is normal text with numbers 123 and symbols !@#$%^*()-_=+[]{}|;:,./? but no dangerous chars";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("This is normal text with numbers 123 and symbols !@#$%^*()-_=+[]{}|;:,./? but no dangerous chars", result);
        }

        [Fact]
        public void SanitizeForLogging_HandlesMixedDangerousContent()
        {
            // Arrange
            string input = "<img src=\"x\" onerror=\"alert('XSS')\"/>\nLog injection\r\nSecond line\t\0null";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            string expected = "&lt;img src=&quot;x&quot; onerror=&quot;alert(&#x27;XSS&#x27;)&quot;/&gt;\\nLog injection\\r\\nSecond line\\t\\0null";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizeForLogging_HandlesLogInjectionAttempt()
        {
            // Arrange
            string input = "Normal log entry\n2024-01-01 [ERROR] Fake error injected by attacker";

            // Act
            string result = input.SanitizeForLogging();

            // Assert
            Assert.Equal("Normal log entry\\n2024-01-01 [ERROR] Fake error injected by attacker", result);
        }
    }
}