namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for managing database transactions directly.
/// This abstraction allows Application layer to manage transactions without depending on Infrastructure.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Executes an action within a transaction.
    /// If an exception occurs, the transaction is rolled back.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
