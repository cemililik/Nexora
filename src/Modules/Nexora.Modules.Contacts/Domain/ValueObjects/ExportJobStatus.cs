namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>Represents the processing status of a contact export job.</summary>
public enum ExportJobStatus
{
    /// <summary>Export has been queued but not yet picked up by the worker.</summary>
    Queued,

    /// <summary>Worker is currently generating the export file.</summary>
    Processing,

    /// <summary>Export file is available for download.</summary>
    Completed,

    /// <summary>Export failed; see <c>ErrorDetails</c> for the lockey.</summary>
    Failed
}
