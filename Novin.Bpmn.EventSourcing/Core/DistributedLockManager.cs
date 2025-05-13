using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.EventSourcing.Core
{
    /// <summary>
    /// Provides asynchronous distributed locking capabilities with timeout and fallback mechanisms.
    /// </summary>
    public class DistributedLockManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly SemaphoreSlim _dictionaryLock = new(1, 1);
        private readonly ILogger<DistributedLockManager> _logger;
        private readonly TimeSpan _defaultTimeout;
        private readonly TimeSpan _cleanupInterval;
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        public DistributedLockManager(
            ILogger<DistributedLockManager> logger,
            TimeSpan? defaultTimeout = null,
            TimeSpan? cleanupInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(10);
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(10);
            
            // Start cleanup timer to remove unused locks
            _cleanupTimer = new Timer(CleanupUnusedLocks, null, _cleanupInterval, _cleanupInterval);
        }
        
        /// <summary>
        /// Acquires a lock for the specified key with a timeout. If the lock can't be acquired,
        /// a dummy releaser is returned to maintain code flow without blocking.
        /// </summary>
        /// <param name="key">The key to lock on</param>
        /// <param name="timeout">Optional timeout (uses default if not specified)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A disposable object that releases the lock when disposed</returns>
        public async Task<IAsyncDisposable> AcquireLockAsync(
            string key, 
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Lock key cannot be empty", nameof(key));
                
            if (_disposed)
                throw new ObjectDisposedException(nameof(DistributedLockManager));
                
            // Use the specified timeout or the default
            var actualTimeout = timeout ?? _defaultTimeout;
            
            // Get or create the semaphore for this key
            var semaphore = await GetOrCreateSemaphoreAsync(key);
            
            try
            {
                _logger.LogDebug("Attempting to acquire lock for key '{Key}' with timeout {Timeout}ms", 
                    key, actualTimeout.TotalMilliseconds);
                
                // Try to acquire the lock with the specified timeout
                bool acquired = await semaphore.WaitAsync(actualTimeout, cancellationToken);
                
                if (acquired)
                {
                    _logger.LogDebug("Successfully acquired lock for key '{Key}'", key);
                    
                    // Return a disposable that releases the lock when disposed
                    return new AsyncLockReleaser(key, semaphore, _logger);
                }
                
                _logger.LogWarning("Timeout acquiring lock for key '{Key}'. Continuing without lock.", key);
                
                // Return a dummy releaser that does nothing when disposed
                return DummyLockReleaser.Instance;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Lock acquisition for key '{Key}' was canceled", key);
                return DummyLockReleaser.Instance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring lock for key '{Key}'", key);
                return DummyLockReleaser.Instance;
            }
        }
        
        private async Task<SemaphoreSlim> GetOrCreateSemaphoreAsync(string key)
        {
            // Fast path: check if lock already exists
            if (_locks.TryGetValue(key, out var existingSemaphore))
                return existingSemaphore;
                
            // Slow path: create new lock with dictionary lock
            await _dictionaryLock.WaitAsync();
            try
            {
                return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            }
            finally
            {
                _dictionaryLock.Release();
            }
        }
        
        private void CleanupUnusedLocks(object? state)
        {
            if (_disposed) return;
            
            try
            {
                if (!_dictionaryLock.Wait(0)) return; // Don't block if lock is in use
                
                try
                {
                    int count = 0;
                    
                    // Remove semaphores that are unused (CurrentCount == 1)
                    foreach (var key in _locks.Keys)
                    {
                        if (_locks.TryGetValue(key, out var semaphore) && 
                            semaphore.CurrentCount == 1)
                        {
                            if (_locks.TryRemove(key, out var removedSemaphore))
                            {
                                removedSemaphore.Dispose();
                                count++;
                            }
                        }
                    }
                    
                    if (count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} unused locks", count);
                    }
                }
                finally
                {
                    _dictionaryLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during lock cleanup");
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _cleanupTimer.Dispose();
            
            foreach (var semaphore in _locks.Values)
            {
                semaphore.Dispose();
            }
            
            _locks.Clear();
            _dictionaryLock.Dispose();
        }
        
        /// <summary>
        /// Disposable that releases a lock when disposed
        /// </summary>
        private class AsyncLockReleaser : IAsyncDisposable, IDisposable
        {
            private readonly string _key;
            private readonly SemaphoreSlim _semaphore;
            private readonly ILogger _logger;
            private bool _disposed;
            
            public AsyncLockReleaser(string key, SemaphoreSlim semaphore, ILogger logger)
            {
                _key = key;
                _semaphore = semaphore;
                _logger = logger;
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                
                _disposed = true;
                _semaphore.Release();
                _logger.LogDebug("Released lock for key '{Key}'", _key);
            }
            
            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
} 