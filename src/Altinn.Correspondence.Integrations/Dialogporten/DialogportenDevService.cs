using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten
{
    public class DialogportenDevService : IDialogportenService
    {
        private readonly ICorrespondenceRepository _correspondenceRepository;

        public DialogportenDevService(ICorrespondenceRepository correspondenceRepository)
        {
            _correspondenceRepository = correspondenceRepository;
        }

        public async Task<string> CreateCorrespondenceDialog(Guid correspondenceId, bool skipUnreadTrigger = false)
        {
            var dialogId = Guid.NewGuid().ToString();
            await _correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
            return dialogId;
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
