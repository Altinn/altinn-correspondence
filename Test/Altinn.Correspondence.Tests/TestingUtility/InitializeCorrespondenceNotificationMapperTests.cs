using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Mappers;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class InitializeCorrespondenceNotificationMapperTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapToRequest_ShouldMapUseStaleContactInformation(bool useStaleContactInformation)
    {
        // Arrange
        var notificationExt = new InitializeCorrespondenceNotificationExt
        {
            NotificationTemplate = NotificationTemplateExt.GenericAltinnMessage,
            NotificationChannel = NotificationChannelExt.Email,
            UseStaleContactInformation = useStaleContactInformation
        };

        // Act
        var result = InitializeCorrespondenceNotificationMapper.MapToRequest(notificationExt);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(useStaleContactInformation, result.AsT0.UseStaleContactInformation);
    }
}
