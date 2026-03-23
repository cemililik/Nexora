namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>Represents the processing status of a contact import job.</summary>
public enum ImportJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
