using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Altinn.Correspondence.Tests.TestingHandler
{
    public class CorrespondenceDueDateHandlerTests
    {
        [Fact]
        public async Task Process_ThrowsNotImplementedException()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<CorrespondenceDueDateHandler>>();
            var correspondenceRepositoryMock = new Mock<ICorrespondenceRepository>();
            var eventBusMock = new Mock<IEventBus>();
            var handler = new CorrespondenceDueDateHandler(loggerMock.Object, correspondenceRepositoryMock.Object, eventBusMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<NotImplementedException>(() => handler.Process(Guid.NewGuid()));
        }
    }
}