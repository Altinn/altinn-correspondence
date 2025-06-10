using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class CreateDialogActivityRequestMapper
    {
        internal static CreateDialogActivityRequest CreateDialogActivityRequest(CorrespondenceEntity correspondence, DialogportenActorType actorType, DialogportenTextType? textType, ActivityType type, DateTimeOffset activityTimestamp, params string[] tokens)
        {
            var dialogActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(); // Dialogporten requires time-stamped GUIDs, not supported natively until .NET 9.0
            var urnActorId = actorType switch
            {
                DialogportenActorType.ServiceOwner => null,
                DialogportenActorType.Sender => correspondence.GetSenderUrn(),
                DialogportenActorType.Recipient => correspondence.GetRecipientUrn(),
                _ => throw new NotImplementedException()
            };

            var createDialogActivityRequest = new CreateDialogActivityRequest()
            {
                Id = dialogActivityId,
                CreatedAt = activityTimestamp,
                PerformedBy = new ActivityPerformedBy()
                {
                    ActorType = actorType == DialogportenActorType.ServiceOwner ? "ServiceOwner" : "PartyRepresentative",
                    ActorId = urnActorId
                },
                Type = type
            };

            if (type == ActivityType.Information)
            {
                if (textType is null)
                {
                    throw new ArgumentException("TextType must be set when creating an information activity");
                }
                createDialogActivityRequest.Description = new List<ActivityDescription>()
                {
                    new ActivityDescription()
                    {
                        LanguageCode = "nb",
                        Value = DialogportenText.GetDialogportenText(textType.Value, DialogportenLanguageCode.NB, tokens)
                    },
                    new ActivityDescription()
                    {
                        LanguageCode = "nn",
                        Value = DialogportenText.GetDialogportenText(textType.Value, DialogportenLanguageCode.NN, tokens)
                    },
                    new ActivityDescription()
                    {
                        LanguageCode = "en",
                        Value = DialogportenText.GetDialogportenText(textType.Value, DialogportenLanguageCode.EN, tokens)
                    }
                };
            }
            else 
            {
                createDialogActivityRequest.Description = new();
            }

            return createDialogActivityRequest;
        }
    }
}
