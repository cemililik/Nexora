namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Unit of Work abstraction. Each module's DbContext implements this.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
