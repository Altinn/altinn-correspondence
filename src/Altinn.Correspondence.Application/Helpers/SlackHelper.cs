
using System.Threading.Tasks;
using Slack.Webhooks;

namespace Altinn.Correspondence.Helpers
{
    /// <summary>
    /// Helper class for Slack notification functionality
    /// </summary>
    public class SlackHelper
    {
        public static async Task<bool> SendSlackNotificationWithMessage(string title, string message, ISlackClient slackClient, string Channel, string hostEnvironment)
        {
            Console.WriteLine("Sending slack message: " + message);
            var text = $":warning: *{title}*\n" +
              $"*Environment:* {hostEnvironment}\n" +
              $"*System:* Correspondence\n" +
              $"*Message:* {message}\n" +
              $"*Time:* {DateTime.UtcNow:u}\n";
            var slackMessage = new SlackMessage
            {
                Text = text,
                Channel = Channel,
            };
            try
            {
                await slackClient.PostAsync(slackMessage);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
