using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Helpers;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class CreateDialogActivityRequestMapper
    {
        internal static CreateDialogActivityRequest CreateDialogActivityRequest(CorrespondenceEntity correspondence, DialogportenActorType actorType, DialogportenTextType? textType, ActivityType type, params string[] tokens)
        {
            return CreateDialogActivityRequest(correspondence, actorType, textType, type, DateTime.UtcNow, tokens);
        }
        internal static CreateDialogActivityRequest CreateDialogActivityRequest(CorrespondenceEntity correspondence, DialogportenActorType actorType, DialogportenTextType? textType, ActivityType type, DateTimeOffset dateOfDialog, params string[] tokens)
        {
            var dialogActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(); // Dialogporten requires time-stamped GUIDs, not supported natively until .NET 9.0
            var urnActorId = actorType switch
            {
                DialogportenActorType.ServiceOwner => null,
                DialogportenActorType.Sender => correspondence.GetSenderUrn(),
                DialogportenActorType.Recipient => correspondence.GetRecipientUrn(),
                _ => throw new NotImplementedException()
            };
            CreateDialogActivityRequest createDialogActivityRequest;
            if (TransmissionValidator.IsTransmission(correspondence) && type == ActivityType.TransmissionOpened)
            {
                var transmissionId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenTransmissionId)?.ReferenceValue;
                if (transmissionId == null)
                {
                    throw new ArgumentException("Correspondence does not have a Dialogporten Transmission Id reference");
                }
                createDialogActivityRequest = new CreateDialogActivityRequest()
                {
                    Id = dialogActivityId,
                    CreatedAt = dateOfDialog,
                    PerformedBy = new ActivityPerformedBy()
                    {
                        ActorType = actorType == DialogportenActorType.ServiceOwner ? "ServiceOwner" : "PartyRepresentative",
                        ActorId = urnActorId
                    },
                    Type = type,
                    TransmissionId = transmissionId
                };
            }
            else
            {
                createDialogActivityRequest = new CreateDialogActivityRequest()
                {
                    Id = dialogActivityId,
                    CreatedAt = dateOfDialog,
                    PerformedBy = new ActivityPerformedBy()
                    {
                        ActorType = actorType == DialogportenActorType.ServiceOwner ? "ServiceOwner" : "PartyRepresentative",
                        ActorId = urnActorId
                    },
                    Type = type
                };
            }

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
