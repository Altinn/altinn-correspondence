using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{

    public static class DialogportenText
    {
        public static string GetDialogportenText(DialogportenTextType type, DialogportenLanguageCode languageCode, params string[] tokens) => languageCode switch
        {
            DialogportenLanguageCode.NB => GetNBText(type, tokens),
            DialogportenLanguageCode.NN => GetNNText(type, tokens),
            DialogportenLanguageCode.EN => GetENText(type, tokens),
            _ => throw new ArgumentException("Invalid language code")
        };

        private static string GetNBText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Varslingsordre opprettet.",
            DialogportenTextType.NotificationOrderCancelled => "Varslingsordre kansellert.",  
            DialogportenTextType.DownloadStarted => string.Format("Startet nedlastning av vedlegg {0}", tokens),
            DialogportenTextType.CorrespondencePublished => "Melding publisert.",
            DialogportenTextType.CorrespondenceConfirmed => "Melding bekreftet.",
            DialogportenTextType.CorrespondencePurged => "Melding slettet.",
            _ => throw new ArgumentException("Invalid text type")
        };

        private static string GetNNText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Varslingsordre opprettet.",
            DialogportenTextType.NotificationOrderCancelled => "Varslingsordre kansellert.",
            DialogportenTextType.DownloadStarted => string.Format("Startet nedlastning av vedlegg {0}", tokens),
            DialogportenTextType.CorrespondencePublished => "Melding publisert.",
            DialogportenTextType.CorrespondenceConfirmed => "Melding bekreftet.",
            DialogportenTextType.CorrespondencePurged => "Melding slettet.",
            _ => throw new ArgumentException("Invalid text type")
        };

        private static string GetENText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Notification order created.",
            DialogportenTextType.NotificationOrderCancelled => "Notification order cancelled.",
            DialogportenTextType.DownloadStarted => string.Format("Started downloading attachment {0}", tokens),
            DialogportenTextType.CorrespondencePublished => "Message published.",
            DialogportenTextType.CorrespondenceConfirmed => "Message confirmed.",
            DialogportenTextType.CorrespondencePurged => "Message deleted.",
            _ => throw new ArgumentException("Invalid text type")
        };
    }
}
