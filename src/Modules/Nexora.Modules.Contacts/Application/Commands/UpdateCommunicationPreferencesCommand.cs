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

/// <summary>A single channel preference entry for upsert.</summary>
public sealed record ChannelPreference(string Channel, bool OptedIn, string? OptInSource = null);

/// <summary>Command to upsert communication preferences for a contact.</summary>
public sealed record UpdateCommunicationPreferencesCommand(
    Guid ContactId,
    IReadOnlyList<ChannelPreference> Preferences) : ICommand<IReadOnlyList<CommunicationPreferenceDto>>;

/// <summary>Validates communication preferences update input.</summary>
public sealed class UpdateCommunicationPreferencesValidator : AbstractValidator<UpdateCommunicationPreferencesCommand>
{
    public UpdateCommunicationPreferencesValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_contacts_validation_contact_id_required");

        RuleFor(x => x.Preferences)
            .NotEmpty().WithMessage("lockey_contacts_validation_preferences_required");

        RuleForEach(x => x.Preferences).ChildRules(pref =>
        {
            pref.RuleFor(p => p.Channel)
                .NotEmpty().WithMessage("lockey_contacts_validation_channel_required")
                .Must(c => Enum.TryParse<CommunicationChannel>(c, out _))
                .WithMessage("lockey_contacts_validation_channel_invalid");
        });
    }
}

/// <summary>Upserts communication preferences for a contact.</summary>
public sealed class UpdateCommunicationPreferencesHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateCommunicationPreferencesHandler> logger) : ICommandHandler<UpdateCommunicationPreferencesCommand, IReadOnlyList<CommunicationPreferenceDto>>
{
    public async Task<Result<IReadOnlyList<CommunicationPreferenceDto>>> Handle(
        UpdateCommunicationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found for tenant {TenantId}", request.ContactId, tenantId);
            return Result<IReadOnlyList<CommunicationPreferenceDto>>.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        var existing = await dbContext.CommunicationPreferences
            .Where(p => p.ContactId == contactId)
            .ToListAsync(cancellationToken);

        var result = new List<CommunicationPreferenceDto>();

        foreach (var pref in request.Preferences)
        {
            var channel = Enum.Parse<CommunicationChannel>(pref.Channel);
            var current = existing.FirstOrDefault(e => e.Channel == channel);

            if (current is not null)
            {
                if (pref.OptedIn)
                    current.OptIn(pref.OptInSource);
                else
                    current.OptOut();
            }
            else
            {
                current = CommunicationPreference.Create(contactId, channel, pref.OptedIn, pref.OptInSource);
                await dbContext.CommunicationPreferences.AddAsync(current, cancellationToken);
            }

            result.Add(MapToDto(current));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Communication preferences updated for contact {ContactId}", contactId);

        return Result<IReadOnlyList<CommunicationPreferenceDto>>.Success(result,
            LocalizedMessage.Of("lockey_contacts_preferences_updated"));
    }

    private static CommunicationPreferenceDto MapToDto(CommunicationPreference p) => new(
        p.Id.Value, p.ContactId.Value, p.Channel.ToString(),
        p.OptedIn, p.OptedInAt, p.OptedOutAt, p.OptInSource);
}
