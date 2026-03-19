using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to add a note to a contact.</summary>
public sealed record AddContactNoteCommand(
    Guid ContactId,
    Guid AuthorUserId,
    string Content) : ICommand<ContactNoteDto>;

/// <summary>Validates note creation input.</summary>
public sealed class AddContactNoteValidator : AbstractValidator<AddContactNoteCommand>
{
    public AddContactNoteValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.AuthorUserId)
            .NotEmpty().WithMessage("lockey_contacts_validation_author_user_id_required");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("lockey_contacts_validation_note_content_required")
            .MaximumLength(5000).WithMessage("lockey_contacts_validation_note_content_max_length");
    }
}

/// <summary>Creates a note on a contact.</summary>
public sealed class AddContactNoteHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddContactNoteHandler> logger) : ICommandHandler<AddContactNoteCommand, ContactNoteDto>
{
    public async Task<Result<ContactNoteDto>> Handle(
        AddContactNoteCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<ContactNoteDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var note = ContactNote.Create(contactId, request.AuthorUserId, orgId, request.Content);

        await dbContext.ContactNotes.AddAsync(note, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Note {NoteId} added to contact {ContactId}", note.Id, contactId);

        var dto = new ContactNoteDto(
            note.Id.Value, note.ContactId.Value, note.AuthorUserId,
            note.Content, note.IsPinned, note.CreatedAt, note.UpdatedAt);

        return Result<ContactNoteDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_note_added"));
    }
}
