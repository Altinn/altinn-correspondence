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
            var existingCorrespondenceContent = await _context.CorrespondenceContents.Include(correspondenceContent => correspondenceContent.Attachments).FirstOrDefaultAsync(correspondenceContent => correspondenceContent.CorrespondenceId == correspondenceId);
            if (existingCorrespondenceContent is null)
            {
                throw new Exception("Invalid state");
            }
            var correspondenceAttachment = await _context.CorrespondenceAttachments
                .FirstOrDefaultAsync(ca => ca.CorrespondenceContentId == existingCorrespondenceContent.Id && ca.AttachmentId == attachmentId, cancellationToken);

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
            var existingCorrespondenceContent = await _context.CorrespondenceContents.Include(correspondenceContent => correspondenceContent.Attachments).FirstOrDefaultAsync(correspondenceContent => correspondenceContent.CorrespondenceId == correspondenceId);
            if (existingCorrespondenceContent is null)
            {
                throw new Exception("Invalid state");
            }
            var existingCorrespondenceAttachment = existingCorrespondenceContent.Attachments.FirstOrDefault(correspondenceAttachment => correspondenceAttachment.AttachmentId == attachmentId);
            if (existingCorrespondenceAttachment is not null)
            {
                return attachmentId;
            }

            // TODO, need to revamp CorrespondenceAttachmentEntity to include less information
            var correspondenceAttachment = new CorrespondenceAttachmentEntity
            {
                CorrespondenceContentId = existingCorrespondenceContent.Id,
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
