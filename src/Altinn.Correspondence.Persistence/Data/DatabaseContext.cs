using Altinn.Correspondence.Core.Models;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

public class ApplicationDbContext : DbContext
{
    private string? _accessToken;
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
        var conn = this.Database.GetDbConnection();
        if (IsAccessTokenValid())
        {
            var connectionString = conn.ConnectionString;
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions());
            var token = credential
                .GetToken(
                    new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" })
                );
            _accessToken = token.Token;
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            connectionStringBuilder.Password = token.Token;
            conn.ConnectionString = connectionStringBuilder.ToString();
        }

    }
    public DbSet<AttachmentEntity> Attachments { get; set; }
    public DbSet<AttachmentStatusEntity> AttachmentStatuses { get; set; }
    public DbSet<CorrespondenceAttachmentEntity> CorrespondenceAttachments { get; set; }
    public DbSet<CorrespondenceContentEntity> CorrespondenceContents { get; set; }
    public DbSet<CorrespondenceEntity> Correspondences { get; set; }
    public DbSet<CorrespondenceStatusEntity> CorrespondenceStatuses { get; set; }
    public DbSet<CorrespondenceNotificationEntity> CorrespondenceNotifications { get; set; }
    public DbSet<CorrespondenceNotificationStatusEntity> CorrespondenceNotificationStatuses { get; set; }
    public DbSet<CorrespondenceReplyOptionEntity> CorrespondenceReplyOptions { get; set; }
    public DbSet<ExternalReferenceEntity> ExternalReferences { get; set; }

    private bool IsAccessTokenValid()
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            return false;
        }
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.ReadToken(_accessToken);
        return DateTime.UtcNow.AddSeconds(60) < token.ValidTo;
    }

}