using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Altinn.Correspondence.Core.Services.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    internal static class SetDialogSystemLabelsMapper
    {
        internal static SetDialogSystemLabelRequest CreateSetDialogSystemLabelsRequestForArchived(
            Guid dialogId,
            string performedByActorId,
            DialogportenActorType performedByactorType)
        {
            return new SetDialogSystemLabelRequest
            {
                AddLabels = new List<Models.SystemLabel>
                {
                    Models.SystemLabel.Archive
                },
                PerformedBy = new Actor
                {
                    ActorType = performedByactorType,
                    ActorId = performedByActorId
                }
            };
        }

        internal static SetDialogSystemLabelRequest CreateSetDialogSystemLabelRequest(
            Guid dialogId,
            string performedByActorId,
            DialogportenActorType performedByactorType,
            List<DialogPortenSystemLabel>? systemLabelsToAdd, List<DialogPortenSystemLabel>? systemLabelsToRemove)
        {
            SetDialogSystemLabelRequest request = new SetDialogSystemLabelRequest
            {
                PerformedBy = new Actor
                {
                    ActorType = performedByactorType,
                    ActorId = performedByActorId
                }
            };

            if (systemLabelsToAdd != null)
            {
                request.AddLabels = new List<Models.SystemLabel>();
                foreach (var systemLabel in systemLabelsToAdd)
                {
                    request.AddLabels = request.AddLabels.Append(MapSystemLabelToExternal(systemLabel)).ToList();
                }
            }
            if(systemLabelsToRemove != null)
            {
                request.RemoveLabels = new List<Models.SystemLabel>(systemLabelsToRemove.Count);
                foreach (var systemLabel in systemLabelsToRemove)
                {
                        request.RemoveLabels = request.RemoveLabels.Append(MapSystemLabelToExternal(systemLabel)).ToList();
                }
            }

            return request;
        }

        private static Models.SystemLabel MapSystemLabelToExternal(DialogPortenSystemLabel label)
        {
            return label switch
            {
                DialogPortenSystemLabel.Archive => Models.SystemLabel.Archive,
                DialogPortenSystemLabel.Bin => Models.SystemLabel.Bin,
                DialogPortenSystemLabel.Default => Models.SystemLabel.Default,
                DialogPortenSystemLabel.MarkedAsUnopened => Models.SystemLabel.MarkedAsUnopened,
                DialogPortenSystemLabel.Sent => Models.SystemLabel.Sent,

                _ => throw new ArgumentOutOfRangeException(nameof(label), $"Not expected system label value: {label}"),
            };
        }
    }   
}
