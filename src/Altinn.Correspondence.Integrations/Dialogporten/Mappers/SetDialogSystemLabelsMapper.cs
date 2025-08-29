using Altinn.Correspondence.Integrations.Dialogporten.Models;
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
            List<string>? systemLabelsToAdd, List<string>? systemLabelsToRemove)
        {
            SetDialogSystemLabelRequest request = new SetDialogSystemLabelRequest
            {
                DialogId = dialogId,
                EnduserId = enduserId,
            };

            if(systemLabelsToAdd != null)
            {
                foreach (var systemLabel in systemLabelsToAdd)
                {
                    if (Enum.TryParse<Models.SystemLabel>(systemLabel, out var parsedLabel))
                    {
                        request.AddLabels = request.AddLabels.Append(parsedLabel).ToList();
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid system label: {systemLabel}");
                    }
                }
            }            
            if(systemLabelsToRemove != null)
            {
                foreach (var systemLabel in systemLabelsToRemove)
                {
                    if (Enum.TryParse<Models.SystemLabel>(systemLabel, out var parsedLabel))
                    {
                        request.RemoveLabels = request.RemoveLabels.Append(parsedLabel).ToList();
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid system label: {systemLabel}");
                    }
                }
            }

            return request;
        }
    }   
}
