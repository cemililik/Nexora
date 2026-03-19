namespace Nexora.SharedKernel.Domain.Base;

/// <summary>
/// Unit of Work abstraction. Each module's DbContext implements this.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
