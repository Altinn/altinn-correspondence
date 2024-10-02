using Slack.Webhooks;

namespace Alinn.Correspondence.Integrations.Slack
{
    internal class SlackDevClient : ISlackClient
    {
        public SlackDevClient()
        {
        }

        public Task<bool> PostAsync(SlackMessage slackMessage)
        {
            return Task.FromResult(true);
        }

        public bool PostToChannels(SlackMessage message, IEnumerable<string> channels)
        {
            return true;
        }

        public IEnumerable<Task<bool>> PostToChannelsAsync(SlackMessage message, IEnumerable<string> channels)
        {
            return [];
        }

        bool ISlackClient.Post(SlackMessage slackMessage)
        {
            return true;
        }
    }
}