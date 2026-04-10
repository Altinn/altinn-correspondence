using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class DialogportenTextTests
{
    [Fact]
    public void GetDialogportenText_DownloadStarted_WithLongFileName_TruncatesToSupportedLength()
    {
        // 221 + 4 extension = 225, which previously produced a 256-char EN message.
        var fileName = new string('a', 221) + ".pdf";

        var nbText = DialogportenText.GetDialogportenText(
            DialogportenTextType.DownloadStarted,
            DialogportenLanguageCode.NB,
            fileName);
        var enText = DialogportenText.GetDialogportenText(
            DialogportenTextType.DownloadStarted,
            DialogportenLanguageCode.EN,
            fileName);

        Assert.True(nbText.Length <= 255, $"Expected NB text length <= 255, got {nbText.Length}");
        Assert.True(enText.Length <= 255, $"Expected EN text length <= 255, got {enText.Length}");
        Assert.Contains("....pdf", enText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDialogportenText_DownloadStarted_WithLongFileName_KeepsExtension()
    {
        var fileName = new string('b', 260) + ".docx";

        var enText = DialogportenText.GetDialogportenText(
            DialogportenTextType.DownloadStarted,
            DialogportenLanguageCode.EN,
            fileName);

        Assert.EndsWith(".docx", enText, StringComparison.Ordinal);
        Assert.Contains("...", enText, StringComparison.Ordinal);
        Assert.True(enText.Length <= 255, $"Expected EN text length <= 255, got {enText.Length}");
    }

    [Fact]
    public void GetDialogportenText_DownloadStarted_WithShortFileName_DoesNotChangeValue()
    {
        const string fileName = "document-22.04.2026.pdf";

        var nbText = DialogportenText.GetDialogportenText(
            DialogportenTextType.DownloadStarted,
            DialogportenLanguageCode.NB,
            fileName);

        Assert.Equal("Startet nedlastning av vedlegg document-22.04.2026.pdf", nbText);
    }
}
