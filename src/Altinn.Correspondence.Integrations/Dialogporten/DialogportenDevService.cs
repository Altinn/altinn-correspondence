using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Newtonsoft.Json;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    public class DialogportenDevService : IDialogportenService
    {
        public Task<string> CreateCorrespondenceDialog(Guid correspondenceId)
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public async Task PatchCorrespondenceDialogToConfirmed(Guid correspondenceId)
        {
            await Task.CompletedTask;
        }

        public Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, DateTimeOffset activityTimestamp, params string[] tokens)
        {
            return Task.CompletedTask;
        }
        
        public Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp)
        {
            return Task.CompletedTask;
        }

        public Task CreateConfirmedActivity(Guid correspondenceId, DialogportenActorType actorType, DateTimeOffset activityTimestamp)
        {
            return Task.CompletedTask;
        }

        public Task PurgeCorrespondenceDialog(Guid correspondenceId)
        {
            return Task.CompletedTask;
        }

        public Task SoftDeleteDialog(string dialogId)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TrySoftDeleteDialog(string dialogId)
        {
            return Task.FromResult(true);
        }

        public Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType,  string actorName, DateTimeOffset activityTimestamp)
        {
            return Task.CompletedTask;
        }

        public Task<string> CreateCorrespondenceDialogForMigratedCorrespondence(Guid correspondenceId, CorrespondenceEntity? correspondence, bool enableEvents = false, bool isSoftDeleted = false)
        {
            // var createDialogRequest = CreateDialogRequestMapper.CreateCorrespondenceDialog(correspondence, "https://platform.tt02.altinn.no/", true);
            // string result = JsonConvert.SerializeObject(createDialogRequest);
            // File.WriteAllText($@"c:\temp\{Guid.NewGuid()}.json", result);
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task UpdateSystemLabelsOnDialog(Guid correspondenceId, string enduserId, List<DialogPortenSystemLabel>? systemLabelsToAdd, List<DialogPortenSystemLabel>? systemLabelsToRemove)
        {
            return Task.CompletedTask;
        }
    }
}
