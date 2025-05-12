using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Contracts
{
    /// <summary>
    /// Result of getting a state with version
    /// </summary>
    /// <typeparam name="T">Type of state</typeparam>
    public class StateWithVersion<T>
    {
        /// <summary>
        /// The state object
        /// </summary>
        public T? State { get; set; }
        
        /// <summary>
        /// Version of the state
        /// </summary>
        public long Version { get; set; }
    }
    
    /// <summary>
    /// قرارداد مخزن حالت نمونه‌های فرآیند BPMN
    /// </summary>
    public interface IProcessInstanceStateStore
    {
        /// <summary>
        /// واکشی وضعیت نمونه فرآیند بر اساس شناسه
        /// </summary>
        Task<ProcessInstanceState?> GetAsync(
            string processInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// درج یا به‌روزرسانی وضعیت نمونه فرآیند
        /// </summary>
        Task SaveAsync(
            ProcessInstanceState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// حذف وضعیت نمونه فرآیند
        /// </summary>
        Task DeleteAsync(
            string processInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// بررسی وجود یک وضعیت نمونه فرآیند
        /// </summary>
        Task<bool> ExistsAsync(
            string processInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// جستجو بر اساس شناسه نمونه فرآیند (instanceId)
        /// </summary>
        Task<IReadOnlyList<ProcessInstanceState>> QueryByInstanceIdAsync(
            string instanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// جستجو بر اساس شناسه استقرار (deploymentId)
        /// </summary>
        Task<IReadOnlyList<ProcessInstanceState>> QueryByDeploymentIdAsync(
            string deploymentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// جستجو بر اساس وضعیت (status)
        /// </summary>
        Task<IReadOnlyList<ProcessInstanceState>> QueryByStatusAsync(
            string status,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// جستجوی انعطاف‌پذیر با استفاده از یک تابع شرطی دلخواه
        /// </summary>

        /// <summary>
        /// Get the state for a process instance
        /// </summary>
        /// <typeparam name="T">Type of state to return</typeparam>
        /// <param name="processInstanceId">Process instance ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>State object or null if not found</returns>
        Task<T?> GetStateAsync<T>(string processInstanceId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get the state for a process instance
        /// </summary>
        /// <param name="processInstanceId">Process instance ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>State object or null if not found</returns>
        Task<object?> GetStateAsync(string processInstanceId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get the state for a process instance with its version
        /// </summary>
        /// <typeparam name="T">Type of state to return</typeparam>
        /// <param name="processInstanceId">Process instance ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>State object with version or null if not found</returns>
        Task<StateWithVersion<T>> GetStateWithVersionAsync<T>(string processInstanceId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Save the state for a process instance
        /// </summary>
        /// <param name="processInstanceId">Process instance ID</param>
        /// <param name="state">State object</param>
        /// <param name="expectedVersion">Expected version (for optimistic concurrency)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveStateAsync(string processInstanceId, object state, long? expectedVersion = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete the state for a process instance
        /// </summary>
        /// <param name="processInstanceId">Process instance ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteStateAsync(string processInstanceId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Find states matching a pattern
        /// </summary>
        /// <typeparam name="T">Type of state to return</typeparam>
        /// <param name="pattern">Instance ID pattern (supports * wildcard)</param>
        /// <param name="predicate">Optional predicate to filter results</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of matching states</returns>
        Task<IReadOnlyList<T>> FindStatesByPatternAsync<T>(
            string pattern, 
            Func<T, bool>? predicate = null, 
            CancellationToken cancellationToken = default);
    }
}
