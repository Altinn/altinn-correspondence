using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Extensions;

public static class CorrespondenceEntityExtensions
{
    // The transmission type is stored when a transmission is initialized, so it is set even when creating the transmission fails.
    // Transmissions initialized before that are recognised by their transmission id.
    public static bool IsTransmission(this CorrespondenceEntity correspondence)
    {
        return correspondence.ExternalReferences.Any(reference =>
            reference.ReferenceType is ReferenceType.DialogportenTransmissionType or ReferenceType.DialogportenTransmissionId);
    }
}
