using System.Text.Json;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Tests.Factories;
using Moq;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class NotificationMapperTests
{
    private readonly NotificationMapper _mapper = new(new Mock<IResourceRegistryService>().Object);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapNotificationV2ToV1Async_ShouldReturnUseStaleContactInformationFromOrderRequest_WhenRecipientIsPerson(bool useStaleContactInformation)
    {
        // Arrange
        var notification = CreateNotification(new RecipientV2
        {
            RecipientPerson = new RecipientPerson
            {
                NationalIdentityNumber = "01017012345",
                ResourceId = "urn:altinn:resource:test-resource-id",
                UseStaleContactInformation = useStaleContactInformation
            }
        });

        // Act
        var result = await _mapper.MapNotificationV2ToV1Async(CreateShipmentStatus(notification), notification);

        // Assert
        Assert.Equal(useStaleContactInformation, result.UseStaleContactInformation);
    }

    [Fact]
    public async Task MapNotificationV2ToV1Async_ShouldReturnNullUseStaleContactInformation_WhenRecipientIsOrganization()
    {
        // Arrange
        var notification = CreateNotification(new RecipientV2
        {
            RecipientOrganization = new RecipientOrganization
            {
                OrgNumber = "991825827",
                ResourceId = "urn:altinn:resource:test-resource-id"
            }
        });

        // Act
        var result = await _mapper.MapNotificationV2ToV1Async(CreateShipmentStatus(notification), notification);

        // Assert
        Assert.Null(result.UseStaleContactInformation);
    }

    [Fact]
    public async Task MapNotificationV2ToV1Async_ShouldReturnNullUseStaleContactInformation_WhenNotificationHasNoOrderRequest()
    {
        // Arrange
        var notification = CreateNotification(recipient: null);

        // Act
        var result = await _mapper.MapNotificationV2ToV1Async(CreateShipmentStatus(notification), notification);

        // Assert
        Assert.Null(result.UseStaleContactInformation);
    }

    private static CorrespondenceNotificationEntity CreateNotification(RecipientV2? recipient)
    {
        var correspondence = new CorrespondenceEntityBuilder().Build();
        return new CorrespondenceNotificationEntity
        {
            CorrespondenceId = correspondence.Id,
            Correspondence = correspondence,
            NotificationTemplate = NotificationTemplate.GenericAltinnMessage,
            NotificationChannel = NotificationChannel.Email,
            Created = DateTimeOffset.UtcNow,
            ShipmentId = Guid.NewGuid(),
            OrderRequest = recipient == null ? null : JsonSerializer.Serialize(new NotificationOrderRequestV2 { Recipient = recipient })
        };
    }

    private static NotificationStatusResponseV2 CreateShipmentStatus(CorrespondenceNotificationEntity notification)
    {
        return new NotificationStatusResponseV2
        {
            ShipmentId = notification.ShipmentId!.Value,
            SendersReference = "test-senders-reference",
            Type = "Notification",
            Status = "Order_Completed",
            LastUpdate = DateTimeOffset.UtcNow
        };
    }
}
