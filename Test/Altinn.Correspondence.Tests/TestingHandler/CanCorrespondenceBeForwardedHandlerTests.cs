using Altinn.Correspondence.Application;
using Altinn.Correspondence.Application.ForwardCorrespondence;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class CanCorrespondenceBeForwardedHandlerTests
{
    private readonly Mock<ICorrespondenceRepository> _correspondenceRepositoryMock = new();
    private readonly Mock<IAttachmentRepository> _attachmentRepositoryMock = new();
    private readonly Mock<IAltinnAuthorizationService> _altinnAuthorizationServiceMock = new();
    private readonly Mock<ILogger<CanCorrespondenceBeForwardedHandler>> _loggerMock = new();

    private readonly CanCorrespondenceBeForwardedHandler _handler;

    public CanCorrespondenceBeForwardedHandlerTests()
    {
        _handler = new CanCorrespondenceBeForwardedHandler(
            _correspondenceRepositoryMock.Object,
            _attachmentRepositoryMock.Object,
            _altinnAuthorizationServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Process_UserIsNull_ReturnsNoAccessToResource()
    {
        var result = await _handler.Process(Guid.NewGuid(), null, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(AuthorizationErrors.NoAccessToResource, result.AsT1);
    }

    [Fact]
    public async Task Process_CorrespondenceNotFound_ReturnsCorrespondenceNotFound()
    {
        var correspondenceId = Guid.NewGuid();
        _correspondenceRepositoryMock
            .Setup(x => x.DoesCorrespondenceAllowForwarding(correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool?)null);

        var result = await _handler.Process(correspondenceId, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.Equal(CorrespondenceErrors.CorrespondenceNotFound, result.AsT1);
    }

    [Fact]
    public async Task Process_AttachmentLookupThrows_PropagatesException()
    {
        var correspondence = SetupCorrespondence(allowForwarding: true);
        _attachmentRepositoryMock
            .Setup(x => x.GetTotalAttachmentSizeByCorrespondence(correspondence.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None));
    }

    [Fact]
    public async Task Process_ForwardingNotAllowed_ReturnsAllowForwardingFalseWithoutCheckingAttachments()
    {
        var correspondence = SetupCorrespondence(allowForwarding: false);

        var result = await _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.AllowForwarding);
        Assert.Equal(ForwardingConstraints.None, result.AsT0.ForwardingConstraints);
        _attachmentRepositoryMock.Verify(x => x.GetTotalAttachmentSizeByCorrespondence(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyAttachmentAccessNeverChecked();
    }

    [Fact]
    public async Task Process_ForwardingAllowedWithoutAccessToAllAttachments_ReturnsNotAccessToAllAttachments()
    {
        var correspondence = SetupCorrespondence(allowForwarding: true, "a.pdf");
        SetupAttachmentAccess(hasAccess: false);

        var result = await _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.AllowForwarding);
        Assert.Equal(ForwardingConstraints.NotAccessToAllAttachments, result.AsT0.ForwardingConstraints);
        _attachmentRepositoryMock.Verify(x => x.GetTotalAttachmentSizeByCorrespondence(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_ForwardingAllowedWithoutAccessToAllAttachmentsAndOver10MB_NotAccessToAllAttachmentsTakesPrecedence()
    {
        var correspondence = SetupCorrespondence(allowForwarding: true, "a.pdf");
        SetupAttachmentAccess(hasAccess: false);
        SetupTotalAttachmentSize(correspondence.Id, 10_000_001);

        var result = await _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ForwardingConstraints.NotAccessToAllAttachments, result.AsT0.ForwardingConstraints);
    }

    [Fact]
    public async Task Process_ForwardingAllowedWithAccessToAllAttachmentsAndOver10MB_ReturnsAttachmentsExceedMaxLimit()
    {
        var correspondence = SetupCorrespondence(allowForwarding: true, "a.pdf", "b.pdf");
        SetupAttachmentAccess(hasAccess: true);
        SetupTotalAttachmentSize(correspondence.Id, 10_000_001);

        var result = await _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(ForwardingConstraints.AttachmentsExceedMaxLimit, result.AsT0.ForwardingConstraints);
        _altinnAuthorizationServiceMock.Verify(x => x.CheckAttachmentAccessAsRecipient(
            It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<AttachmentEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000_000)]
    public async Task Process_ForwardingAllowedWithAttachmentsWithinLimit_ReturnsNoConstraints(long totalAttachmentSize)
    {
        var correspondence = SetupCorrespondence(allowForwarding: true);
        SetupTotalAttachmentSize(correspondence.Id, totalAttachmentSize);

        var result = await _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.AllowForwarding);
        Assert.Equal(ForwardingConstraints.None, result.AsT0.ForwardingConstraints);
    }

    [Fact]
    public async Task Process_ForwardingAllowedWithAttachmentsOver10MB_ReturnsAttachmentsExceedMaxLimit()
    {
        var correspondence = SetupCorrespondence(allowForwarding: true);
        SetupTotalAttachmentSize(correspondence.Id, 10_000_001);

        var result = await _handler.Process(correspondence.Id, CreateUser(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.AllowForwarding);
        Assert.Equal(ForwardingConstraints.AttachmentsExceedMaxLimit, result.AsT0.ForwardingConstraints);
    }

    private CorrespondenceEntity SetupCorrespondence(bool allowForwarding, params string[] attachmentNames)
    {
        var builder = new CorrespondenceEntityBuilder()
            .WithAllowForwarding(allowForwarding);
        foreach (var attachmentName in attachmentNames)
        {
            builder = builder.WithAttachment(attachmentName);
        }
        var correspondence = builder.Build();
        _correspondenceRepositoryMock
            .Setup(x => x.DoesCorrespondenceAllowForwarding(correspondence.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowForwarding);
        _correspondenceRepositoryMock
            .Setup(x => x.GetCorrespondenceById(correspondence.Id, false, true, false, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(correspondence);
        return correspondence;
    }

    private void SetupAttachmentAccess(bool hasAccess)
    {
        _altinnAuthorizationServiceMock
            .Setup(x => x.CheckAttachmentAccessAsRecipient(It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<AttachmentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAccess);
    }

    private void VerifyAttachmentAccessNeverChecked()
    {
        _altinnAuthorizationServiceMock.Verify(x => x.CheckAttachmentAccessAsRecipient(
            It.IsAny<ClaimsPrincipal>(), It.IsAny<CorrespondenceEntity>(), It.IsAny<AttachmentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupTotalAttachmentSize(Guid correspondenceId, long totalAttachmentSize)
    {
        _attachmentRepositoryMock
            .Setup(x => x.GetTotalAttachmentSizeByCorrespondence(correspondenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(totalAttachmentSize);
    }

    private static ClaimsPrincipal CreateUser()
    {
        var identity = new ClaimsIdentity([new Claim("c", $"{UrnConstants.PersonIdAttribute}:01819012012")], "TestAuthType");
        return new ClaimsPrincipal(identity);
    }
}
