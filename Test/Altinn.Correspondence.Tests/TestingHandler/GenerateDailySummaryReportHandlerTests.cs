using Moq;
using Altinn.Correspondence.Application.GenerateReport;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Parquet.Serialization;
using Altinn.Correspondence.Core.Models;

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
            Mock.Of<Hangfire.IBackgroundJobClient>(),
            _mockLogger.Object,
            _mockHostEnvironment.Object);
    }

    [Fact]
    public async Task ProcessAndDownload_ShouldGenerateParquetFileWithCorrectColumnNamesFromGitHubIssue()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        var request = new GenerateDailySummaryReportRequest { Altinn2Included = false };
        var correspondenceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var mainShipmentId1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var mainShipmentId2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var reminderShipmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var mainSent1 = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var mainSent2 = new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);
        var reminderSent = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        
        var correspondenceDailySummaries = new List<DailySummaryDataDto>()
        {
            new DailySummaryDataDto()
            {
                CorrespondenceId = correspondenceId,
                AltinnVersion = Core.Models.Enums.AltinnVersion.Altinn3,
                AttachmentStorageBytes = 0,
                DatabaseStorageBytes = 0,
                Date = DateTime.UtcNow.Date,
                Day = (int)DateTime.UtcNow.DayOfWeek,
                MessageSender = "",
                SenderOrgNumber = "910753614",
                Month = DateTime.UtcNow.Month,
                RecipientType = Core.Models.Enums.RecipientType.Organization,
                ResourceId = "test-resource",
                ServiceOwnerId = "123456789",
                ServiceOwnerName = "Test Service Owner",
                Year = DateTime.UtcNow.Year,
                ShipmentId = mainShipmentId1,
                IsReminder = false,
                NotificationSent = mainSent1
            },
            new DailySummaryDataDto()
            {
                CorrespondenceId = correspondenceId,
                AltinnVersion = Core.Models.Enums.AltinnVersion.Altinn3,
                AttachmentStorageBytes = 0,
                DatabaseStorageBytes = 0,
                Date = DateTime.UtcNow.Date,
                Day = (int)DateTime.UtcNow.DayOfWeek,
                MessageSender = "",
                SenderOrgNumber = "910753614",
                Month = DateTime.UtcNow.Month,
                RecipientType = Core.Models.Enums.RecipientType.Organization,
                ResourceId = "test-resource",
                ServiceOwnerId = "123456789",
                ServiceOwnerName = "Test Service Owner",
                Year = DateTime.UtcNow.Year,
                ShipmentId = mainShipmentId2,
                IsReminder = false,
                NotificationSent = mainSent2
            },
            new DailySummaryDataDto()
            {
                CorrespondenceId = correspondenceId,
                AltinnVersion = Core.Models.Enums.AltinnVersion.Altinn3,
                AttachmentStorageBytes = 0,
                DatabaseStorageBytes = 0,
                Date = DateTime.UtcNow.Date,
                Day = (int)DateTime.UtcNow.DayOfWeek,
                MessageSender = "",
                SenderOrgNumber = "910753614",
                Month = DateTime.UtcNow.Month,
                RecipientType = Core.Models.Enums.RecipientType.Organization,
                ResourceId = "test-resource",
                ServiceOwnerId = "123456789",
                ServiceOwnerName = "Test Service Owner",
                Year = DateTime.UtcNow.Year,
                ShipmentId = reminderShipmentId,
                IsReminder = true,
                NotificationSent = reminderSent
            }
        };
        _mockCorrespondenceRepository.Setup(x => x.StreamDailySummaryBatches(
            It.IsAny<bool>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<int>())).Returns(StreamBatches(correspondenceDailySummaries));

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
        var result = await _handler.ProcessAndDownload(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsT0); // Should be successful response
        
        var response = result.AsT0;
        Assert.NotNull(response.FileStream);
        Assert.Equal(1, response.TotalCorrespondenceCount);
        
        // Verify the parquet file has correct column names
        var columnNames = GetParquetColumnNames(response.FileStream);
        
        var expectedColumnNames = new[]
        {
            "correspondenceid",
            "date",
            "year", 
            "month",
            "day",
            "serviceownerorgnr",
            "serviceownercode", 
            "messagesender",
            "senderorgnr",
            "serviceresourceid",
            "serviceresourcetitle",
            "recipienttype",
            "costcenter",
            "databasestoragebytes",
            "attachmentstoragebytes",
            "shipment_id",
            "is_reminder",
            "notification_sent"
        };

        foreach (var expectedColumn in expectedColumnNames)
        {
            Assert.Contains(expectedColumn, columnNames);
        }

        Assert.DoesNotContain("messagecount", columnNames);
        Assert.DoesNotContain("shipment_ids", columnNames);
        Assert.DoesNotContain("reminder_shipment_ids", columnNames);

        // Verify all columns are lowercase
        foreach (var columnName in columnNames)
        {
            Assert.True(columnName == columnName.ToLowerInvariant(), 
                $"Column name '{columnName}' should be lowercase");
        }

        // Verify sender org number and one row per notification
        response.FileStream.Position = 0;
        var deserializationResult = await ParquetSerializer.DeserializeAsync<ParquetDailySummaryData>(response.FileStream, cancellationToken: CancellationToken.None);
        var rows = deserializationResult.Data;
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("11111111-1111-1111-1111-111111111111", r.CorrespondenceId));
        Assert.All(rows, r => Assert.Equal("910753614", r.SenderOrgNumber));
        Assert.Contains(rows, r => r.ShipmentId == mainShipmentId1.ToString() && r.IsReminder == false && r.NotificationSent == mainSent1.UtcDateTime.ToString("O"));
        Assert.Contains(rows, r => r.ShipmentId == mainShipmentId2.ToString() && r.IsReminder == false && r.NotificationSent == mainSent2.UtcDateTime.ToString("O"));
        Assert.Contains(rows, r => r.ShipmentId == reminderShipmentId.ToString() && r.IsReminder == true && r.NotificationSent == reminderSent.UtcDateTime.ToString("O"));
    }

    [Fact]
    public void ResolveReportMonth_WhenYearAndMonthOmitted_UsesCurrentUtcMonth()
    {
        var before = DateTimeOffset.UtcNow;
        var (year, month) = GenerateDailySummaryReportHandler.ResolveReportMonth(new GenerateDailySummaryReportRequest());
        var after = DateTimeOffset.UtcNow;

        var matchesBefore = year == before.Year && month == before.Month;
        var matchesAfter = year == after.Year && month == after.Month;
        Assert.True(matchesBefore || matchesAfter,
            $"Expected ({before.Year}-{before.Month:D2}) or ({after.Year}-{after.Month:D2}), got ({year}-{month:D2}).");
    }

    [Fact]
    public void ResolveRecurringReportMonth_OnFirstDays_UsesPreviousUtcMonth()
    {
        var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        var (year, month) = GenerateDailySummaryReportHandler.ResolveRecurringReportMonth(now);
        Assert.Equal(2026, year);
        Assert.Equal(8, month);
    }

    [Fact]
    public void ResolveRecurringReportMonth_AfterFirstDays_UsesCurrentUtcMonth()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var (year, month) = GenerateDailySummaryReportHandler.ResolveRecurringReportMonth(now);
        Assert.Equal(2026, year);
        Assert.Equal(9, month);
    }

    [Fact]
    public void ResolveReportMonth_WhenYearAndMonthProvided_UsesThem()
    {
        var (year, month) = GenerateDailySummaryReportHandler.ResolveReportMonth(
            new GenerateDailySummaryReportRequest { Year = 2025, Month = 8 });
        Assert.Equal(2025, year);
        Assert.Equal(8, month);
    }

    [Fact]
    public void BuildMonthlyReportFileName_UsesStableYearMonthName()
    {
        var fileName = GenerateDailySummaryReportHandler.BuildMonthlyReportFileName(2026, 9, false, "Production");
        Assert.Equal("daily_summary_report_202609_A3_Production.parquet", fileName);
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
                Content = new CorrespondenceContentEntity
                {
                    Language = "en",
                    MessageTitle = "Test title",
                    MessageSummary = "Test summary",
                    MessageBody = "Test body",
                    Attachments = new List<CorrespondenceAttachmentEntity>()
                },
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
                Content = new CorrespondenceContentEntity
                {
                    Language = "no",
                    MessageTitle = "Test title 2",
                    MessageSummary = "Test summary 2",
                    MessageBody = "Test body 2",
                    Attachments = new List<CorrespondenceAttachmentEntity>()
                },
                SendersReference = "Test Reference 2",
                RequestedPublishTime = DateTimeOffset.UtcNow.AddDays(-1),
                Statuses = new List<CorrespondenceStatusEntity>(),
                Altinn2CorrespondenceId = 12345
            }
        };
    }

    private static async IAsyncEnumerable<IReadOnlyList<DailySummaryDataDto>> StreamBatches(
        IReadOnlyList<DailySummaryDataDto> batch)
    {
        yield return batch;
        await Task.CompletedTask;
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
