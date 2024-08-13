using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceMapper
{
    internal static InitializeCorrespondenceRequest MapToRequest(InitializeCorrespondenceExt initializeCorrespondenceExt, List<IFormFile>? attachments, bool isUploadRequest)
    {
        var data = InitializeMultipleCorrespondencesMapper.MapToRequest(initializeCorrespondenceExt, null, attachments, isUploadRequest);
        data.Correspondence.Recipient = initializeCorrespondenceExt.Recipient;
        return new InitializeCorrespondenceRequest()
        {
            Correspondence = data.Correspondence,
            Attachments = attachments ?? new List<IFormFile>(),
            isUploadRequest = isUploadRequest
        };
    }
}
