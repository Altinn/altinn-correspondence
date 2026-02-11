using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class TextValidationTests
    {
        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForValidPlainText()
        {
            // Arrange
            string input = "This is a valid plain text without markdown syntax.";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateMarkdown_ShouldReturnTrue_ForValidMarkdown()
        {
            // Arrange
            string input = "This is a **bold** text and this is *italic* text.";
            // Act
            bool result = TextValidation.ValidateMarkdown(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateMarkdown_ShouldReturnTrue_ForCrocodileLinks()
        {
            // Arrange
            string input = "This is a text with a crocodile link to <https://google.com>";
            // Act
            bool result = TextValidation.ValidateMarkdown(input);
            // Assert
            Assert.True(result);
        }
    }
}
