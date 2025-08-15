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

    }
        
}
