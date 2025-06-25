using System.Text.Json;
using Altinn.Correspondence.Core.Models.Enums;
using Xunit;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class NotificationStatusV2ConverterTests
{
    private readonly JsonSerializerOptions _options;

    public NotificationStatusV2ConverterTests()
    {
        _options = new JsonSerializerOptions();
    }

    [Theory]
    [InlineData("Email_New", NotificationStatusV2.Email_New)]
    [InlineData("SMS_Delivered", NotificationStatusV2.SMS_Delivered)]
    [InlineData("Unknown", NotificationStatusV2.Unknown)]
    public void Read_ValidEnumValue_ReturnsCorrectEnum(string input, NotificationStatusV2 expected)
    {
        // Arrange
        var json = $"\"{input}\"";

        // Act
        var result = JsonSerializer.Deserialize<NotificationStatusV2>(json, _options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("email_new", NotificationStatusV2.Email_New)]
    [InlineData("EMAIL_NEW", NotificationStatusV2.Email_New)]
    [InlineData("sms_delivered", NotificationStatusV2.SMS_Delivered)]
    [InlineData("SMS_DELIVERED", NotificationStatusV2.SMS_Delivered)]
    [InlineData("unknown", NotificationStatusV2.Unknown)]
    [InlineData("UNKNOWN", NotificationStatusV2.Unknown)]
    public void Read_CaseInsensitiveEnumValue_ReturnsCorrectEnum(string input, NotificationStatusV2 expected)
    {
        // Arrange
        var json = $"\"{input}\"";

        // Act
        var result = JsonSerializer.Deserialize<NotificationStatusV2>(json, _options);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("InvalidValue")]
    [InlineData("NonExistentStatus")]
    [InlineData("Random_String")]
    [InlineData("123")]
    public void Read_InvalidEnumValue_ReturnsUnknown(string input)
    {
        // Arrange
        var json = $"\"{input}\"";

        // Act
        var result = JsonSerializer.Deserialize<NotificationStatusV2>(json, _options);

        // Assert
        Assert.Equal(NotificationStatusV2.Unknown, result);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("null")]
    public void Read_EmptyOrNullValue_ReturnsUnknown(string json)
    {
        // Act
        var result = JsonSerializer.Deserialize<NotificationStatusV2>(json, _options);

        // Assert
        Assert.Equal(NotificationStatusV2.Unknown, result);
    }

    [Fact]
    public void Read_NonStringJsonToken_ReturnsUnknown()
    {
        // Arrange
        var json = "123"; // Number instead of string

        // Act
        var result = JsonSerializer.Deserialize<NotificationStatusV2>(json, _options);

        // Assert
        Assert.Equal(NotificationStatusV2.Unknown, result);
    }

    [Theory]
    [InlineData(NotificationStatusV2.Email_New, "Email_New")]
    [InlineData(NotificationStatusV2.SMS_Delivered, "SMS_Delivered")]
    [InlineData(NotificationStatusV2.Unknown, "Unknown")]
    public void Write_ValidEnum_SerializesToCorrectString(NotificationStatusV2 input, string expected)
    {
        // Act
        var json = JsonSerializer.Serialize(input, _options);

        // Assert
        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void Roundtrip_ValidValues_MaintainConsistency()
    {
        // Arrange
        var originalValues = new[]
        {
            NotificationStatusV2.Email_New,
            NotificationStatusV2.Email_Succeeded,
            NotificationStatusV2.Email_Delivered,
            NotificationStatusV2.Email_Failed,
            NotificationStatusV2.Email_Failed_TTL,
            NotificationStatusV2.SMS_New,
            NotificationStatusV2.SMS_Accepted,
            NotificationStatusV2.SMS_Delivered,
            NotificationStatusV2.SMS_Failed,
            NotificationStatusV2.SMS_Failed_TTL,
            NotificationStatusV2.Unknown
        };

        foreach (var original in originalValues)
        {
            // Act
            var json = JsonSerializer.Serialize(original, _options);
            var deserialized = JsonSerializer.Deserialize<NotificationStatusV2>(json, _options);

            // Assert
            Assert.Equal(original, deserialized);
        }
    }
} 