using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to pin or unpin a contact note.</summary>
public sealed record PinContactNoteCommand(
    Guid ContactId,
    Guid NoteId,
    bool Pin) : ICommand<ContactNoteDto>;

/// <summary>Pins or unpins a contact note.</summary>
public sealed class PinContactNoteHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<PinContactNoteHandler> logger) : ICommandHandler<PinContactNoteCommand, ContactNoteDto>
{
    public async Task<Result<ContactNoteDto>> Handle(
        PinContactNoteCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);
        var noteId = ContactNoteId.From(request.NoteId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactNoteDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var note = await dbContext.ContactNotes.FirstOrDefaultAsync(
            n => n.Id == noteId && n.ContactId == contactId,
            cancellationToken);

        if (note is null)
        {
            logger.LogWarning("Note {NoteId} not found for contact {ContactId}", request.NoteId, request.ContactId);
            return Result<ContactNoteDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_note_not_found"));
        }

        if (request.Pin)
            note.Pin();
        else
            note.Unpin();

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Note {NoteId} {Action} for contact {ContactId}",
            noteId, request.Pin ? "pinned" : "unpinned", contactId);

        var dto = new ContactNoteDto(
            note.Id.Value, note.ContactId.Value, note.AuthorUserId,
            note.Content, note.IsPinned, note.CreatedAt, note.UpdatedAt);

        return Result<ContactNoteDto>.Success(dto,
            LocalizedMessage.Of(request.Pin
                ? "lockey_contacts_note_pinned"
                : "lockey_contacts_note_unpinned"));
    }
}

/// <summary>Validates pin contact note input.</summary>
public sealed class PinContactNoteCommandValidator : AbstractValidator<PinContactNoteCommand>
{
    public PinContactNoteCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
