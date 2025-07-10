using Altinn.Correspondence.Application.TestRetry;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.Controllers
{
    [ApiController]
    [Route("test")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class TestRetryController(IBackgroundJobClient backgroundJobClient) : ControllerBase
    {
        private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;

        [HttpPost("trigger-retry")]
        public ActionResult TriggerRetryTest()
        {
            try
            {
                var jobId = _backgroundJobClient.Enqueue<TestRetryHandler>(handler => handler.Process(null, CancellationToken.None));
                
                return Ok(new
                {
                    Message = "Test retry job triggered successfully",
                    JobId = jobId,
                    Description = "This job will fail all 10 times, triggering Slack notifications on each failure to demonstrate the spam problem"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Error = "Failed to trigger test retry job",
                    Message = ex.Message
                });
            }
        }
    }
} 