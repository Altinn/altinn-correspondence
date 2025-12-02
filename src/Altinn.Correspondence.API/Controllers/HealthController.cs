using Altinn.Correspondence.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Altinn.Correspondence.Controllers
{
    [ApiController]
    [Route("health")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HealthController(ApplicationDbContext dbContext) : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext = dbContext;

        [HttpGet]
        public async Task<ActionResult> HealthCheckAsync()
        {
            try
            {
                await _dbContext.Database.CanConnectAsync();
                return Ok(new
                {
                    Status = "Healthy",
                    Message = "Environment properly configured and database is accessible"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Message = $"Health check failed: {ex.Message}"
                });
            }
        }

        [HttpGet("db-session-role")]
        public async Task<ActionResult> GetDbSessionReplicationRoleAsync()
        {
            await using var connection = _dbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW session_replication_role;";

            var result = await command.ExecuteScalarAsync();

            return Ok(new
            {
                SessionReplicationRole = result?.ToString() ?? "<null>"
            });
        }

        [HttpGet("/healthz")]
        public ActionResult LivenessCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Message = "Process is responsive"
            });
        }
    }
}
