using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;

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

        public Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, params string[] tokens)
        {
            return Task.CompletedTask;
        }
        
        public Task CreateOpenedActivity(Guid correspondenceId, DialogportenActorType actorType)
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

        public Task CreateCorrespondencePurgedActivity(Guid correspondenceId, DialogportenActorType actorType, string actorName)
        {
            return Task.CompletedTask;
        }
    }
}
