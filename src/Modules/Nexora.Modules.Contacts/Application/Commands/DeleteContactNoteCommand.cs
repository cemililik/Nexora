using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to delete a contact note.</summary>
public sealed record DeleteContactNoteCommand(
    Guid ContactId,
    Guid NoteId) : ICommand;

/// <summary>Deletes a note from a contact.</summary>
public sealed class DeleteContactNoteHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteContactNoteHandler> logger) : ICommandHandler<DeleteContactNoteCommand>
{
    public async Task<Result> Handle(
        DeleteContactNoteCommand request,
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
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var note = await dbContext.ContactNotes.FirstOrDefaultAsync(
            n => n.Id == noteId && n.ContactId == contactId,
            cancellationToken);

        if (note is null)
        {
            logger.LogWarning("Note {NoteId} not found for contact {ContactId}", request.NoteId, request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_note_not_found"));
        }

        dbContext.ContactNotes.Remove(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Note {NoteId} deleted from contact {ContactId}", noteId, contactId);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_note_deleted"));
    }
}

/// <summary>Validates delete contact note input.</summary>
public sealed class DeleteContactNoteCommandValidator : AbstractValidator<DeleteContactNoteCommand>
{
    public DeleteContactNoteCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("lockey_validation_required");
    }
}
