using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {

        public HealthController()
        {
        }

        [HttpGet]
        public async Task<ActionResult> HealthCheckAsync()
        {
            return Ok("Environment properly configured");
        }
    }
}
