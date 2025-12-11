using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Integrations.Dialogporten.Helpers
{
    internal class TransmissionValidator
    {
        internal static bool IsTransmission(CorrespondenceEntity correspondence)
        {
            var transmissionId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenTransmissionId)?.ReferenceValue;
            if (transmissionId == null)
            {
                return false;
            }

            return true;
        }
    }
}
