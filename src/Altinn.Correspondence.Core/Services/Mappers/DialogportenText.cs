namespace Altinn.Correspondence.Core.Dialogporten.Mappers
{
    public enum DialogportenTextType
    {
        NotificationOrderCreated,
        NotificationOrderCancelled,
        DownloadStarted,
        CorrespondencePublished,
        CorrespondenceConfirmed,
        CorrespondenceArchived,
        CorrespondencePurged
    }

    public enum DialogportenLanguageCode
    {
        NB,
        NN,
        EN
    }
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
            DialogportenTextType.DownloadStarted => string.Format("Startet nedlastning av vedlegg {name}", tokens),
            DialogportenTextType.CorrespondencePublished => "Melding publisert.",
            DialogportenTextType.CorrespondenceConfirmed => "Melding bekreftet.",
            DialogportenTextType.CorrespondenceArchived => "Melding arkivert.",
            DialogportenTextType.CorrespondencePurged => "Melding slettet.",
        };
        private static string GetNNText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Varslingsordre opprettet.",
            DialogportenTextType.NotificationOrderCancelled => "Varslingsordre kansellert.",
            DialogportenTextType.DownloadStarted => string.Format("Startet nedlastning av vedlegg {name}", tokens),
            DialogportenTextType.CorrespondencePublished => "Melding publisert.",
            DialogportenTextType.CorrespondenceConfirmed => "Melding bekreftet.",
            DialogportenTextType.CorrespondenceArchived => "Melding arkivert.",
            DialogportenTextType.CorrespondencePurged => "Melding slettet.",
        };
        private static string GetENText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Notification order created.",
            DialogportenTextType.NotificationOrderCancelled => "Notification order cancelled.",
            DialogportenTextType.DownloadStarted => string.Format("Started downloading attachment {name}", tokens),
            DialogportenTextType.CorrespondencePublished => "Message published.",
            DialogportenTextType.CorrespondenceConfirmed => "Message confirmed.",
            DialogportenTextType.CorrespondenceArchived => "Message archived.",
            DialogportenTextType.CorrespondencePurged => "Message deleted.",
        };
    }
}
