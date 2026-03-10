using Altinn.Correspondence.Application.Helpers;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class MessageBodyHelpersTests
{
    [Fact]
    public void ConvertMixedToMarkdown_ShouldMakeRelativeHrefAbsolute()
    {
        // Arrange
        const string input =
            "<p>Vedlagt er et brev fra tjeneste-eier. </p><p>Vårt <a style=\"display:inline;\" href=\"/Pages/ServiceEngine/Start/StartService.aspx?ServiceEditionCode=123&ServiceCode=1234\">svarskjema</a> kan brukes til å svare på brevet.</p><p>Klikk på lenken under for å lese brevet:</p>";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, true);

        // Assert
        Assert.True(1 == 2, result);
        Assert.Contains("https://altinn.no/Pages/ServiceEngine/Start/StartService.aspx?ServiceEditionCode=123&ServiceCode=1234", result);
    }

    [Fact]
    public void ConvertMixedToMarkdown_ShouldNotChangeAbsoluteHref()
    {
        // Arrange
        const string input =
            "<p>Se mer informasjon på <a href=\"https://altinn.no/Pages/info\">Altinn</a>.</p>";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, true);

        // Assert
        Assert.True(1 == 2, result);
        Assert.Contains("https://altinn.no/Pages/info", result);
        Assert.DoesNotContain("https://altinn.no/https://altinn.no/Pages/info", result);
    }

    [Fact]
    public void ConvertMixedToMarkdown_ShouldNotChangeMailtoHref()
    {
        // Arrange
        const string input =
            "<p>Kontakt oss på <a href=\"mailto:test@example.com\">epost</a>.</p>";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, true);

        // Assert
        Assert.Contains("mailto:test@example.com", result);
    }

    [Fact]
    public void ConvertMixedToMarkdown_ShouldHandleMarkdownInput()
    {
        // Arrange
        const string input = "Se vårt [svarskjema](/path/to/form).";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, true);

        // Assert
        Assert.Contains("https://altinn.no/path/to/form", result);
    }

    [Fact]
    public void ConvertMixedToMarkdown_ShouldReturnEmptyString_ForEmptyInput()
    {
        // Arrange
        const string input = "";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, true);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertMixedToMarkdown_ShouldNotMakeRelativeHrefAbsolute_ForNonLegacy()
    {
        // Arrange
        const string input =
            "<p>Vedlagt er et brev fra tjeneste-eier. </p><p>Vårt <a style=\"display:inline;\" href=\"/Pages/ServiceEngine/Start/StartService.aspx?ServiceEditionCode=123&ServiceCode=1234\">svarskjema</a> kan brukes til å svare på brevet.</p><p>Klikk på lenken under for å lese brevet:</p>";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, false);

        // Assert
        Assert.DoesNotContain("https://altinn.no/Pages/ServiceEngine/Start/StartService.aspx?ServiceEditionCode=123&ServiceCode=1234", result);
    }

    [Fact]
    public void ConvertMixedToMarkdown_ShouldNotChangeHrefOnNonAnchorTags()
    {
        // Arrange
        const string input =
            "<p>Tekst før.</p><link href=\"/styles/site.css\" rel=\"stylesheet\" /><p>Tekst etter.</p>";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, true);

        // Assert
        Assert.DoesNotContain("https://altinn.no/styles/site.css", result);
    }
    
    [Fact]
    public void ConvertMixedToMarkdown_ShouldPreserveExistingAbsoluteHref_EndToEnd()
    {
        // Arrange: HTML with an absolute href
        const string input =
            "<p>Se mer informasjon på <a href=\"https://altinn.no/Pages/info\">Altinn</a>.</p>";

        // Act
        var result = MessageBodyHelpers.ConvertMixedToMarkdown(input, isLegacy: true);

        // Assert: URL is present and not duplicated or altered
        Assert.Contains("https://altinn.no/Pages/info", result);
        Assert.DoesNotContain("https://altinn.no/https://altinn.no/Pages/info", result);
    }
}
