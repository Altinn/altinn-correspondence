using Altinn.Correspondence.Core.Models.Dialogporten;
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

        public Task CreateInformationActivity(Guid correspondenceId, DialogportenActorType actorType, DialogportenTextType textType, params string[] tokens)
        {
            return Task.CompletedTask;
        }

        public Task PatchData(string dialogId, PatchData data)
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
    }
}
