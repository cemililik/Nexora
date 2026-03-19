namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>Field selections for merge operation.</summary>
public sealed record MergeFieldSelections(
    bool UseSecondaryEmail = false,
    bool UseSecondaryPhone = false);
