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

/// <summary>Command to update a contact note's content.</summary>
public sealed record UpdateContactNoteCommand(
    Guid ContactId,
    Guid NoteId,
    string Content) : ICommand<ContactNoteDto>;

/// <summary>Validates note update input.</summary>
public sealed class UpdateContactNoteValidator : AbstractValidator<UpdateContactNoteCommand>
{
    public UpdateContactNoteValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.NoteId)
            .NotEmpty().WithMessage("lockey_contacts_validation_note_id_required");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("lockey_contacts_validation_note_content_required")
            .MaximumLength(5000).WithMessage("lockey_contacts_validation_note_content_max_length");
    }
}

/// <summary>Updates a contact note's content.</summary>
public sealed class UpdateContactNoteHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateContactNoteHandler> logger) : ICommandHandler<UpdateContactNoteCommand, ContactNoteDto>
{
    public async Task<Result<ContactNoteDto>> Handle(
        UpdateContactNoteCommand request,
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

        note.Update(request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Note {NoteId} updated for contact {ContactId}", noteId, contactId);

        var dto = new ContactNoteDto(
            note.Id.Value, note.ContactId.Value, note.AuthorUserId,
            note.Content, note.IsPinned, note.CreatedAt, note.UpdatedAt);

        return Result<ContactNoteDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_note_updated"));
    }
}
