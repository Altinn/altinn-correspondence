using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Mappers;
using Altinn.Correspondence.Tests.Extensions;
using Altinn.Register.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class PerformedByResolverTests
{
    private const string Sender = $"{UrnConstants.OrganizationNumberAttribute}:991825827";

    [Fact]
    public async Task ResolveAsync_ServiceOwnerAndPartyRepresentative_MapsName()
    {
        var serviceOwnerUuid = Guid.NewGuid();
        var representativeUuid = Guid.NewGuid();
        var register = new Mock<IAltinnRegisterService>();
        register
            .Setup(service => service.LookUpPartiesByIds(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Party>
            {
                RegisterServiceMockExtensions.BuildOrganization(serviceOwnerUuid, "991825827", displayName: "SKATTEETATEN"),
                RegisterServiceMockExtensions.BuildPerson(representativeUuid, "10108000398", displayName: "OLA NORDMANN")
            });

        var result = await PerformedByResolver.ResolveAsync(
            register.Object,
            [serviceOwnerUuid, representativeUuid, Guid.Empty, serviceOwnerUuid],
            Sender,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(PerformedByActorType.ServiceOwner, result[serviceOwnerUuid].ActorType);
        Assert.Equal("Skatteetaten", result[serviceOwnerUuid].ActorName);
        Assert.Equal(PerformedByActorType.PartyRepresentative, result[representativeUuid].ActorType);
        Assert.Equal("Ola Nordmann", result[representativeUuid].ActorName);

        register.Verify(service => service.LookUpPartiesByIds(
            It.Is<List<string>>(ids =>
                ids.Count == 2
                && ids.Contains(serviceOwnerUuid.ToString())
                && ids.Contains(representativeUuid.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_RecipientOrganization_IsPartyRepresentative()
    {
        var recipientUuid = Guid.NewGuid();
        var register = new Mock<IAltinnRegisterService>();
        register
            .Setup(service => service.LookUpPartiesByIds(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Party>
            {
                RegisterServiceMockExtensions.BuildOrganization(recipientUuid, "123456785", displayName: "Mottaker AS")
            });

        var result = await PerformedByResolver.ResolveAsync(
            register.Object,
            [recipientUuid],
            Sender,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(PerformedByActorType.PartyRepresentative, result[recipientUuid].ActorType);
        Assert.Equal("Mottaker As", result[recipientUuid].ActorName);
    }

    [Fact]
    public async Task ResolveAsync_WhenLookupFails_ReturnsEmpty()
    {
        var register = new Mock<IAltinnRegisterService>();
        register
            .Setup(service => service.LookUpPartiesByIds(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("register down"));

        var result = await PerformedByResolver.ResolveAsync(
            register.Object,
            [Guid.NewGuid()],
            Sender,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public void MapListToExternal_IncludesPerformedByOnEachStatusEvent()
    {
        var serviceOwnerUuid = Guid.NewGuid();
        var representativeUuid = Guid.NewGuid();
        var statuses = new List<CorrespondenceStatusEntity>
        {
            new()
            {
                Status = CorrespondenceStatus.Published,
                StatusText = "Published",
                StatusChanged = DateTimeOffset.UtcNow,
                PartyUuid = serviceOwnerUuid
            },
            new()
            {
                Status = CorrespondenceStatus.Read,
                StatusText = "Read",
                StatusChanged = DateTimeOffset.UtcNow,
                PartyUuid = representativeUuid
            },
            new()
            {
                Status = CorrespondenceStatus.Fetched,
                StatusText = "Fetched",
                StatusChanged = DateTimeOffset.UtcNow,
                PartyUuid = Guid.NewGuid()
            }
        };
        var performedBy = new Dictionary<Guid, PerformedBy>
        {
            [serviceOwnerUuid] = new()
            {
                ActorType = PerformedByActorType.ServiceOwner,
                ActorName = "Skatteetaten"
            },
            [representativeUuid] = new()
            {
                ActorType = PerformedByActorType.PartyRepresentative,
                ActorName = "Ola Nordmann"
            }
        };

        var mapped = CorrespondenceStatusMapper.MapListToExternal(statuses, performedBy);

        var serviceOwner = mapped[0].PerformedBy;
        var representative = mapped[1].PerformedBy;
        Assert.NotNull(serviceOwner);
        Assert.Equal(ActorTypeExt.ServiceOwner, serviceOwner.ActorType);
        Assert.Equal("Skatteetaten", serviceOwner.ActorName);
        Assert.NotNull(representative);
        Assert.Equal(ActorTypeExt.PartyRepresentative, representative.ActorType);
        Assert.Equal("Ola Nordmann", representative.ActorName);
        Assert.Null(mapped[2].PerformedBy);
    }
}
