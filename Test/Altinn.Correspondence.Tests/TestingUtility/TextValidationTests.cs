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

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForTextParsedAsMarkdownList()
        {
            // Arrange
            string input = "123. This is valid plaintext even if it contains Markdown list";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForTextContainingAsteriskAndUnderscore()
        {
            // Arrange
            string input = "This *is not* markdown and _should_ be treated as plain text.";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForTextContainingBackticks()
        {
            // Arrange
            string input = "Here is some `inline code` in plain text.";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForTextWithNumberedLikeMarkdownButSentence()
        {
            // Arrange
            string input = "1.234 is a number, not a markdown list.";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForTextWithHashSymbolNotHeading()
        {
            // Arrange
            string input = "Error #404 not found.";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnFalse_ForActualMarkdownHeading()
        {
            // Arrange
            string input = "# This is a markdown heading";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForTextWithExtraWhitespace()
        {
            // Arrange
            string input = "  This is   plain   text with   irregular   spaces.  ";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForEmptyString()
        {
            // Arrange
            string input = string.Empty;
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidatePlainText_ShouldReturnTrue_ForWhitespaceOnly()
        {
            // Arrange
            string input = "   \n\t  ";
            // Act
            bool result = TextValidation.ValidatePlainText(input);
            // Assert
            Assert.True(result);
        }
    }
}
