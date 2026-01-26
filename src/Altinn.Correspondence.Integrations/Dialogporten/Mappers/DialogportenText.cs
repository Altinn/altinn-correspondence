using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{

    public static class DialogportenText
    {
        public static string GetDialogportenText(DialogportenTextType type, DialogportenLanguageCode languageCode, params string[] tokens)
        {
            var normalizedTokens = NormalizeTokens(type, languageCode, tokens);

            return languageCode switch
            {
                DialogportenLanguageCode.NB => GetNBText(type, normalizedTokens),
                DialogportenLanguageCode.NN => GetNNText(type, normalizedTokens),
                DialogportenLanguageCode.EN => GetENText(type, normalizedTokens),
                _ => throw new ArgumentException("Invalid language code")
            };
        }

        private static string GetNBText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Varslingsordre opprettet.",
            DialogportenTextType.NotificationOrderCancelled => "Varslingsordre kansellert.",
            DialogportenTextType.NotificationSent => tokens.Length >= 2 ? string.Format("Varsel om mottatt melding sendt til {0} på {1}.", tokens) : throw new ArgumentException("NotificationSent expects two tokens (destination, channel)"),
            DialogportenTextType.NotificationReminderSent => tokens.Length >= 2 ? string.Format("Revarsel om mottatt melding sendt til {0} på {1}.", tokens) : throw new ArgumentException("NotificationReminderSent expects two tokens (destination, channel)"),
            DialogportenTextType.DownloadStarted => string.Format("Startet nedlastning av vedlegg {0}", tokens),
            DialogportenTextType.CorrespondencePublished => "Melding publisert.",
            DialogportenTextType.CorrespondenceConfirmed => "Melding bekreftet.",
            _ => throw new ArgumentException("Invalid text type")
        };

        private static string GetNNText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Varslingsordre opprettet.",
            DialogportenTextType.NotificationOrderCancelled => "Varslingsordre kansellert.",
            DialogportenTextType.NotificationSent => tokens.Length >= 2 ? string.Format("Varsel om mottatt melding sendt til {0} på {1}.", tokens) : throw new ArgumentException("NotificationSent expects two tokens (destination, channel)"),
            DialogportenTextType.NotificationReminderSent => tokens.Length >= 2 ? string.Format("Revarsel om mottatt melding sendt til {0} på {1}.", tokens) : throw new ArgumentException("NotificationReminderSent expects two tokens (destination, channel)"),
            DialogportenTextType.DownloadStarted => string.Format("Startet nedlastning av vedlegg {0}", tokens),
            DialogportenTextType.CorrespondencePublished => "Melding publisert.",
            DialogportenTextType.CorrespondenceConfirmed => "Melding bekreftet.",
            _ => throw new ArgumentException("Invalid text type")
        };

        private static string GetENText(DialogportenTextType type, params string[] tokens) => type switch
        {
            DialogportenTextType.NotificationOrderCreated => "Notification order created.",
            DialogportenTextType.NotificationOrderCancelled => "Notification order cancelled.",
            DialogportenTextType.NotificationSent => tokens.Length >= 2 ? string.Format("Notification about received message sent to {0} on {1}.", tokens) : throw new ArgumentException("NotificationSent expects two tokens (destination, channel)"),
            DialogportenTextType.NotificationReminderSent => tokens.Length >= 2 ? string.Format("Reminder notification about received message sent to {0} on {1}.", tokens) : throw new ArgumentException("NotificationReminderSent expects two tokens (destination, channel)"),
            DialogportenTextType.DownloadStarted => string.Format("Started downloading attachment {0}", tokens),
            DialogportenTextType.CorrespondencePublished => "Message published.",
            DialogportenTextType.CorrespondenceConfirmed => "Message confirmed.",
            _ => throw new ArgumentException("Invalid text type")
        };

        private static string[] NormalizeTokens(DialogportenTextType type, DialogportenLanguageCode languageCode, string[] tokens)
        {
            if (type is not (DialogportenTextType.NotificationSent or DialogportenTextType.NotificationReminderSent) || tokens.Length < 2)
            {
                return tokens;
            }

            var destination = tokens[0];
            var channel = NormalizeNotificationChannel(channel: tokens[1], languageCode);
            return [destination, channel];
        }

        private static string NormalizeNotificationChannel(string channel, DialogportenLanguageCode languageCode)
        {
            var isEmail =
                string.Equals(channel, "Email", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, "e-post", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, "epost", StringComparison.OrdinalIgnoreCase);

            if (isEmail)
            {
                return languageCode == DialogportenLanguageCode.EN ? "Email" : "e-post";
            }

            var isSms =
                string.Equals(channel, "Sms", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, "SMS", StringComparison.OrdinalIgnoreCase);

            if (isSms)
            {
                return "SMS";
            }

            return channel;
        }
    }
}
