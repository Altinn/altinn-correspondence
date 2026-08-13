using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingApplication;

public class InitializeCorrespondenceHelperTests
{
    [Fact]
    public async Task MapToCorrespondenceEntity_ReferencesInitializedAttachmentById()
    {
        var serviceOwnerHelper = new ServiceOwnerHelper(
            Mock.Of<IServiceOwnerRepository>(),
            NullLogger<ServiceOwnerHelper>.Instance);
        var helper = new InitializeCorrespondenceHelper(
            Mock.Of<IAttachmentRepository>(),
            null!,
            null!,
            serviceOwnerHelper,
            NullLogger<InitializeCorrespondenceHelper>.Instance);
        var attachmentId = Guid.NewGuid();
        var attachment = new AttachmentEntity
        {
            Id = attachmentId,
            ResourceId = "test-resource",
            FileName = "attachment.txt",
            SendersReference = "attachment-reference",
            Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
            Created = DateTimeOffset.UtcNow,
        };
        var request = new InitializeCorrespondencesRequest
        {
            Correspondence = new CorrespondenceEntity
            {
                ResourceId = "test-resource",
                Recipient = string.Empty,
                Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827",
                SendersReference = "correspondence-reference",
                Content = new CorrespondenceContentEntity
                {
                    Language = "nb",
                    MessageTitle = "Title",
                    MessageSummary = "Summary",
                    MessageBody = "Body",
                    Attachments = [],
                },
                RequestedPublishTime = DateTimeOffset.UtcNow,
                Created = DateTimeOffset.UtcNow,
                Statuses = [],
            },
            Recipients = ["urn:altinn:person:identifier-no:14886498226"],
        };

        var result = await helper.MapToCorrespondenceEntityAsync(
            request,
            request.Recipients.Single(),
            [attachment],
            Guid.NewGuid(),
            null,
            false,
            "991825827",
            CancellationToken.None);

        var correspondenceAttachment = Assert.Single(result.Content.Attachments);
        Assert.Equal(attachmentId, correspondenceAttachment.AttachmentId);
        Assert.Null(correspondenceAttachment.Attachment);
    }
}
