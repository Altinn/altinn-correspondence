using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class ExternalReferenceMapper
{
    internal static ExternalReferenceEntity MapToEntity(ExternalReferenceExt externalReferenceExt)
    {
        var externalReference = new ExternalReferenceEntity
        {
            ReferenceValue = externalReferenceExt.ReferenceValue,
            ReferenceType = (ReferenceType)externalReferenceExt.ReferenceType
        };
        return externalReference;
    }
    internal static List<ExternalReferenceEntity> MapListToEntities(List<ExternalReferenceExt> externalReferencesExt)
    {
        var externalReferences = new List<ExternalReferenceEntity>();
        foreach (var extRef in externalReferencesExt)
        {
            externalReferences.Add(MapToEntity(extRef));
        }
        return externalReferences;
    }
}
