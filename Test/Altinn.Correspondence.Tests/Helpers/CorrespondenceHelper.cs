using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.Tests.Helpers;
internal static class CorrespondenceHelper
{
    public static async Task<InitializedCorrespondencesExt> GetInitializedCorrespondence(HttpClient client, JsonSerializerOptions serializerOptions, InitializeCorrespondencesExt payload)
    {
        var initResponse = await client.PostAsJsonAsync("correspondence/api/v1/correspondence", payload);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);
        var correspondence = await initResponse.Content.ReadFromJsonAsync<InitializeCorrespondencesResponseExt>(serializerOptions);
        Assert.NotNull(correspondence?.Correspondences.First());
        return correspondence.Correspondences.First();
    }

    public static MultipartFormDataContent CorrespondenceToFormData(BaseCorrespondenceExt correspondence)
    {
        var formData = new MultipartFormDataContent(){
            { new StringContent(correspondence.ResourceId), "correspondence.resourceId" },
            { new StringContent(correspondence.Sender), "correspondence.sender" },
            { new StringContent(correspondence.SendersReference), "correspondence.sendersReference" },
            { new StringContent(correspondence.RequestedPublishTime.ToString()), "correspondence.RequestedPublishTime" },
            { new StringContent(correspondence.DueDateTime.ToString()), "correspondence.dueDateTime" },
            { new StringContent(correspondence.AllowSystemDeleteAfter.ToString()), "correspondence.AllowSystemDeleteAfter" },
            { new StringContent(correspondence.Content.MessageTitle), "correspondence.content.MessageTitle" },
            { new StringContent(correspondence.Content.MessageSummary), "correspondence.content.MessageSummary" },
            { new StringContent(correspondence.Content.MessageBody), "correspondence.content.MessageBody" },
            { new StringContent(correspondence.Content.Language), "correspondence.content.Language" },
            { new StringContent((correspondence.IgnoreReservation ?? false).ToString()), "correspondence.IgnoreReservation" },
        };
        if (correspondence.Notification != null)
        {
            formData.Add(new StringContent(correspondence.Notification.NotificationTemplate.ToString()), "correspondence.Notification.NotificationTemplate");
            formData.Add(new StringContent(correspondence.Notification.SendReminder.ToString()), "correspondence.Notification.SendReminder");
            if (correspondence.Notification.RequestedSendTime != null) formData.Add(new StringContent(correspondence.Notification.RequestedSendTime.ToString()), "correspondence.Notification.RequestedSendTime");
            if (correspondence.Notification.EmailBody != null) formData.Add(new StringContent(correspondence.Notification.EmailBody), "correspondence.Notification.EmailBody");
            if (correspondence.Notification.EmailSubject != null) formData.Add(new StringContent(correspondence.Notification.EmailSubject), "correspondence.Notification.EmailSubject");
            if (correspondence.Notification.ReminderEmailBody != null) formData.Add(new StringContent(correspondence.Notification.ReminderEmailBody), "correspondence.Notification.ReminderEmailBody");
            if (correspondence.Notification.ReminderEmailSubject != null) formData.Add(new StringContent(correspondence.Notification.ReminderEmailSubject), "correspondence.Notification.ReminderEmailSubject");
            if (correspondence.Notification.SmsBody != null) formData.Add(new StringContent(correspondence.Notification.SmsBody), "correspondence.Notification.SmsBody");
            if (correspondence.Notification.ReminderSmsBody != null) formData.Add(new StringContent(correspondence.Notification.ReminderSmsBody), "correspondence.Notification.ReminderSmsBody");
        }

        correspondence.Content.Attachments.Select((attachment, index) => new[]
        {
            new { Key = $"correspondence.content.Attachments[{index}].DataLocationType", Value = attachment.DataLocationType.ToString() },
            new { Key = $"correspondence.content.Attachments[{index}].FileName", Value = attachment.FileName ?? "" },
            new { Key = $"correspondence.content.Attachments[{index}].SendersReference", Value = attachment.SendersReference },
            new { Key = $"correspondence.content.Attachments[{index}].IsEncrypted", Value = attachment.IsEncrypted.ToString() }
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.ExternalReferences?.Select((externalReference, index) => new[]
        {
            new { Key = $"correspondence.ExternalReference[{index}].ReferenceType", Value = externalReference.ReferenceType.ToString() },
            new { Key = $"correspondence.ExternalReference[{index}].ReferenceValue", Value = externalReference.ReferenceValue },
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.ReplyOptions.Select((replyOption, index) => new[]
        {
            new { Key = $"correspondence.ReplyOptions[{index}].LinkURL", Value = replyOption.LinkURL },
            new { Key = $"correspondence.ReplyOptions[{index}].LinkText", Value = replyOption.LinkText ?? "" }
        }).SelectMany(x => x).ToList()
        .ForEach(item => formData.Add(new StringContent(item.Value), item.Key));

        correspondence.PropertyList.ToList()
        .ForEach((item) => formData.Add(new StringContent(item.Value), "correspondence.propertyLists." + item.Key));
        return formData;
    }

    public static async Task<CorrespondenceOverviewExt> WaitForCorrespondenceStatusUpdate(HttpClient client, JsonSerializerOptions responseSerializerOptions, Guid correspondenceId, CorrespondenceStatusExt expectedStatus, int maxRetries = 4, int delayMs = 2000)
    {
        await Task.Delay(1000);
        for (int i = 0; i < maxRetries; i++)
        {
            var correspondence = await client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", responseSerializerOptions);

            if (correspondence?.Status == expectedStatus)
            {
                return correspondence;
            }

            if (correspondence?.Status == CorrespondenceStatusExt.Failed)
            {
                Assert.Fail($"Correspondence failed with status: {correspondence.Status}");
            }

            await Task.Delay(delayMs);
        }

        // If we get here, the status didn't update within the expected time
        var finalCorrespondence = await client.GetFromJsonAsync<CorrespondenceOverviewExt>($"correspondence/api/v1/correspondence/{correspondenceId}", responseSerializerOptions);
        Assert.NotNull(finalCorrespondence);
        Assert.Fail($"Correspondence status did not update to {expectedStatus} within {maxRetries * delayMs}ms. Current status: {finalCorrespondence?.Status}");
        return finalCorrespondence;
    }
}