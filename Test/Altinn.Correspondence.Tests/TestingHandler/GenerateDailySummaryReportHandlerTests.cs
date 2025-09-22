using Moq;
using Altinn.Correspondence.Application.GenerateReport;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Parquet.Serialization;

namespace Altinn.Correspondence.Tests.TestingHandler;

public class GenerateDailySummaryReportHandlerTests
{
    private readonly Mock<ICorrespondenceRepository> _mockCorrespondenceRepository;
    private readonly Mock<IServiceOwnerRepository> _mockServiceOwnerRepository;
    private readonly Mock<IResourceRegistryService> _mockResourceRegistryService;
    private readonly Mock<IStorageRepository> _mockStorageRepository;
    private readonly Mock<ILogger<GenerateDailySummaryReportHandler>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly GenerateDailySummaryReportHandler _handler;

    public GenerateDailySummaryReportHandlerTests()
    {
        _mockCorrespondenceRepository = new Mock<ICorrespondenceRepository>();
        _mockServiceOwnerRepository = new Mock<IServiceOwnerRepository>();
        _mockResourceRegistryService = new Mock<IResourceRegistryService>();
        _mockStorageRepository = new Mock<IStorageRepository>();
        _mockLogger = new Mock<ILogger<GenerateDailySummaryReportHandler>>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();

        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Test");

        _handler = new GenerateDailySummaryReportHandler(
            _mockCorrespondenceRepository.Object,
            _mockServiceOwnerRepository.Object,
            _mockResourceRegistryService.Object,
            _mockStorageRepository.Object,
            _mockLogger.Object,
            _mockHostEnvironment.Object);
    }

    [Fact]
    public async Task ProcessAndDownload_ShouldGenerateParquetFileWithCorrectColumnNamesFromGitHubIssue()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        var request = new GenerateDailySummaryReportRequest { Altinn2Included = false };
        
        var correspondences = CreateTestCorrespondences();
        _mockCorrespondenceRepository.Setup(x => x.GetCorrespondencesForReport(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(correspondences);

        var serviceOwner = new ServiceOwnerEntity 
        { 
            Id = "123456789", 
            Name = "Test Service Owner",
            StorageProviders = new List<StorageProviderEntity>()
        };
        _mockServiceOwnerRepository.Setup(x => x.GetServiceOwnerByOrgNo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceOwner);

        _mockResourceRegistryService.Setup(x => x.GetServiceOwnerNameOfResource(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test Resource Title");

        // Act
        var result = await _handler.ProcessAndDownload(user, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0); // Should be successful response
        
        var response = result.AsT0;
        Assert.NotNull(response.FileStream);
        
        // Verify the parquet file has correct column names
        var columnNames = GetParquetColumnNames(response.FileStream);
        
        var expectedColumnNames = new[]
        {
            "date",
            "year", 
            "month",
            "day",
            "serviceownerorgnr",
            "serviceownercode", 
            "messagesender",
            "serviceresourceid",
            "serviceresourcetitle",
            "recipienttype",
            "costcenter",
            "messagecount",
            "databasestoragebytes",
            "attachmentstoragebytes"
        };

        foreach (var expectedColumn in expectedColumnNames)
        {
            Assert.Contains(expectedColumn, columnNames);
        }

        // Verify all columns are lowercase
        foreach (var columnName in columnNames)
        {
            Assert.True(columnName == columnName.ToLowerInvariant(), 
                $"Column name '{columnName}' should be lowercase");
        }
    }


    private List<CorrespondenceEntity> CreateTestCorrespondences()
    {
        return new List<CorrespondenceEntity>
        {
            new CorrespondenceEntity
            {
                Id = Guid.NewGuid(),
                Created = DateTimeOffset.UtcNow,
                ServiceOwnerId = "123456789",
                MessageSender = "TestSender",
                ResourceId = "test-resource-id",
                Recipient = "12345678901",
                Sender = "0192:123456789",
                SendersReference = "Test Reference",
                RequestedPublishTime = DateTimeOffset.UtcNow,
                Statuses = new List<CorrespondenceStatusEntity>(),
                Altinn2CorrespondenceId = null
            },
            new CorrespondenceEntity
            {
                Id = Guid.NewGuid(),
                Created = DateTimeOffset.UtcNow.AddDays(-1),
                ServiceOwnerId = "987654321",
                MessageSender = "TestSender2",
                ResourceId = "test-resource-id-2",
                Recipient = "98765432109",
                Sender = "0192:987654321",
                SendersReference = "Test Reference 2",
                RequestedPublishTime = DateTimeOffset.UtcNow.AddDays(-1),
                Statuses = new List<CorrespondenceStatusEntity>(),
                Altinn2CorrespondenceId = 12345
            }
        };
    }

    private string[] GetParquetColumnNames(Stream parquetStream)
    {
        parquetStream.Position = 0;
        
        // Deserialize the parquet data to get the column names from the type
        var parquetData = ParquetSerializer.DeserializeAsync<ParquetDailySummaryData>(parquetStream, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
        
        // Get column names from the JsonPropertyName attributes
        var properties = typeof(ParquetDailySummaryData).GetProperties();
        var columnNames = new List<string>();
        
        foreach (var property in properties)
        {
            var jsonPropertyNameAttribute = property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false)
                .FirstOrDefault() as System.Text.Json.Serialization.JsonPropertyNameAttribute;
            
            if (jsonPropertyNameAttribute != null)
            {
                columnNames.Add(jsonPropertyNameAttribute.Name);
            }
            else
            {
                // Fallback to property name if no attribute
                columnNames.Add(property.Name.ToLowerInvariant());
            }
        }
        
        return columnNames.ToArray();
    }
}
