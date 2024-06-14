using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Repositories
{
    public class CorrespondenceAttachmentRepository(ApplicationDbContext context) : ICorrespondenceAttachmentRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Guid> PurgeCorrespondenceAttachmentsByAttachmentId(Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondenceAttachments = await _context.CorrespondenceAttachments
                .Where(ca => ca.AttachmentId == attachmentId)
                .ToListAsync(cancellationToken);

            _context.RemoveRange(correspondenceAttachments);
            await _context.SaveChangesAsync(cancellationToken);

            return attachmentId;
        }

        public async Task<Guid> RemoveAttachmentFromCorrespondence(Guid correspondenceId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var correspondenceAttachment = await _context.CorrespondenceAttachments
                .Where(ca => ca.Id == correspondenceId && ca.AttachmentId == attachmentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (correspondenceAttachment == null)
            {
                return Guid.Empty;
            }

            _context.Remove(correspondenceAttachment);
            await _context.SaveChangesAsync(cancellationToken);

            return attachmentId;
        }   

        public async Task<Guid> AddAttachmentToCorrespondence(Guid correspondenceId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var attachment = await _context.Attachments.FindAsync(attachmentId);
            if (attachment is null || attachment.FileName is null)
            {
                return Guid.Empty;
            }

            // TODO, need to revamp CorrespondenceAttachmentEntity to include less information
            var correspondenceAttachment = new CorrespondenceAttachmentEntity
            {
                CorrespondenceContentId = correspondenceId,
                AttachmentId = attachmentId,
                DataType = attachment.DataType,
                IntendedPresentation = IntendedPresentationType.MachineReadable,
                Name = attachment.FileName,
                SendersReference = attachment.SendersReference,
            };

            _context.Add(correspondenceAttachment);
            await _context.SaveChangesAsync(cancellationToken);

            return attachmentId;
        }
    }
}
