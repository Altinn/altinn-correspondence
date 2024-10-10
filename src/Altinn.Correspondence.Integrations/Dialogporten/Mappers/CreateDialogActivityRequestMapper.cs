using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using UUIDNext;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    internal class CreateDialogActivityRequestMapper
    {
        internal static CreateDialogActivityRequest CreateDialogActivityRequest(CorrespondenceEntity correspondence, DialogportenActorType actorType, string description, string? extendedType, ActivityType activityType = ActivityType.Information)
        {
            var dialogActivityId = Uuid.NewDatabaseFriendly(Database.PostgreSql).ToString(); // Dialogporten requires time-stamped GUIDs, not supported natively until .NET 9.0
            var urnActorId = actorType switch
            {
                DialogportenActorType.ServiceOwner => null,
                DialogportenActorType.Sender => correspondence.GetSenderUrn(),
                DialogportenActorType.Recipient => correspondence.GetRecipientUrn(),
                _ => throw new NotImplementedException()
            };
            return new CreateDialogActivityRequest()
            {
                Id = dialogActivityId,
                CreatedAt = DateTime.UtcNow,
                Description = new List<ActivityDescription>()
                {
                    new ActivityDescription()
                    {
                        LanguageCode = "nb",
                        Value = description
                    }
                },
                ExtendedType = extendedType,
                PerformedBy = new ActivityPerformedBy()
                {
                    ActorType = actorType == DialogportenActorType.ServiceOwner ? "ServiceOwner" : "PartyRepresentative",
                    ActorId = urnActorId
                },
                Type = activityType
            };
        }
    }
}
