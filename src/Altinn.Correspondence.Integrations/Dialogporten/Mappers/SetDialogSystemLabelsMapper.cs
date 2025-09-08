using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Integrations.Dialogporten.Models;
using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    internal static class SetDialogSystemLabelsMapper
    {
        internal static SetDialogSystemLabelRequest CreateSetDialogSystemLabelsRequestForArchived(
            Guid dialogId,
            string enduserId)
        {
            return new SetDialogSystemLabelRequest
            {
                DialogId = dialogId,
                EnduserId = enduserId,
                AddLabels = new List<Models.SystemLabel>
                {
                    Models.SystemLabel.Archive
                },
            };
        }

        internal static SetDialogSystemLabelRequest CreateSetDialogSystemLabelRequest(
            Guid dialogId,
            string enduserId,
            List<DialogPortenSystemLabel>? systemLabelsToAdd, List<DialogPortenSystemLabel>? systemLabelsToRemove)
        {
            SetDialogSystemLabelRequest request = new SetDialogSystemLabelRequest
            {
                DialogId = dialogId,
                EnduserId = enduserId,
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
