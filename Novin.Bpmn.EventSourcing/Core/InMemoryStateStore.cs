using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پیاده‌سازی درون‌حافظه‌ای مخزن وضعیت
/// </summary>
public class InMemoryStateStore : IStateStore
{
    private class StateContainer
    {
        public long Version { get; set; }
        public string? Json { get; set; }
    }
    
    private readonly ConcurrentDictionary<string, StateContainer> _states = new();
    private readonly ILogger<InMemoryStateStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از مخزن وضعیت درون‌حافظه‌ای
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public InMemoryStateStore(ILogger<InMemoryStateStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public Task<T?> GetStateAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
        try
        {
            if (_states.TryGetValue(key, out var container) && container.Json != null)
            {
                var state = JsonSerializer.Deserialize<T>(container.Json, _jsonOptions);
                return Task.FromResult(state);
            }
            
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing state for key {Key}", key);
            throw new StateStoreException($"Error retrieving state for key {key}", ex);
        }
    }

    /// <inheritdoc />
    public Task<(T? State, long Version)> GetStateWithVersionAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
        try
        {
            if (_states.TryGetValue(key, out var container) && container.Json != null)
            {
                var state = JsonSerializer.Deserialize<T>(container.Json, _jsonOptions);
                return Task.FromResult((state, container.Version));
            }
            
            return Task.FromResult<(T?, long)>((null, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing state for key {Key}", key);
            throw new StateStoreException($"Error retrieving state for key {key}", ex);
        }
    }

    /// <inheritdoc />
    public Task<long> SaveStateAsync<T>(string key, T state, long? expectedVersion = null, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
        if (state == null)
            throw new ArgumentNullException(nameof(state));
            
        try
        {
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            
            long newVersion;
            
            if (expectedVersion.HasValue)
            {
                // با استفاده از قفل خوش‌بینانه (optimistic locking)
                if (_states.TryGetValue(key, out var existing))
                {
                    if (existing.Version != expectedVersion.Value)
                    {
                        throw new ConcurrencyException($"Concurrency conflict for key {key}. Expected version {expectedVersion} but current version is {existing.Version}");
                    }
                    
                    newVersion = existing.Version + 1;
                    var updated = new StateContainer { Version = newVersion, Json = json };
                    
                    if (!_states.TryUpdate(key, updated, existing))
                    {
                        throw new StateStoreException($"Failed to update state for key {key}");
                    }
                }
                else if (expectedVersion.Value == 0)
                {
                    // ایجاد وضعیت جدید
                    newVersion = 1;
                    var newContainer = new StateContainer { Version = newVersion, Json = json };
                    
                    if (!_states.TryAdd(key, newContainer))
                    {
                        throw new ConcurrencyException($"Concurrency conflict for key {key}. State was added by another thread.");
                    }
                }
                else
                {
                    throw new ConcurrencyException($"Concurrency conflict for key {key}. State does not exist but expected version is {expectedVersion}");
                }
            }
            else
            {
                // بدون بررسی نسخه
                if (_states.TryGetValue(key, out var existing))
                {
                    // بروزرسانی وضعیت موجود
                    newVersion = existing.Version + 1;
                    var updated = new StateContainer { Version = newVersion, Json = json };
                    
                    if (!_states.TryUpdate(key, updated, existing))
                    {
                        throw new StateStoreException($"Failed to update state for key {key}");
                    }
                }
                else
                {
                    // ایجاد وضعیت جدید
                    newVersion = 1;
                    var newContainer = new StateContainer { Version = newVersion, Json = json };
                    
                    if (!_states.TryAdd(key, newContainer))
                    {
                        throw new StateStoreException($"Failed to add state for key {key}");
                    }
                }
            }
            
            _logger.LogDebug("Saved state for key {Key} with version {Version}", key, newVersion);
            return Task.FromResult(newVersion);
        }
        catch (ConcurrencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving state for key {Key}", key);
            throw new StateStoreException($"Error saving state for key {key}", ex);
        }
    }

    /// <inheritdoc />
    public Task DeleteStateAsync(string key, long? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
        try
        {
            if (expectedVersion.HasValue)
            {
                // با استفاده از قفل خوش‌بینانه
                if (_states.TryGetValue(key, out var existing))
                {
                    if (existing.Version != expectedVersion.Value)
                    {
                        throw new ConcurrencyException($"Concurrency conflict for key {key}. Expected version {expectedVersion} but current version is {existing.Version}");
                    }
                    
                    if (!_states.TryRemove(key, out _))
                    {
                        throw new StateStoreException($"Failed to delete state for key {key}");
                    }
                }
                else if (expectedVersion.Value > 0)
                {
                    throw new ConcurrencyException($"Concurrency conflict for key {key}. State does not exist but expected version is {expectedVersion}");
                }
            }
            else
            {
                // بدون بررسی نسخه
                _states.TryRemove(key, out _);
            }
            
            _logger.LogDebug("Deleted state for key {Key}", key);
            return Task.CompletedTask;
        }
        catch (ConcurrencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting state for key {Key}", key);
            throw new StateStoreException($"Error deleting state for key {key}", ex);
        }
    }

    /// <inheritdoc />
    public Task<bool> HasStateAsync(string key)
    {
        var exists = _states.ContainsKey(key);
        _logger.LogDebug("Checked existence of key {Key}: {Exists}", key, exists);
        return Task.FromResult(exists);
    }
    
    /// <inheritdoc />
    public Task<long> GetVersionAsync(string key)
    {
        if (_states.TryGetValue(key, out var entry))
        {
            _logger.LogDebug("Retrieved version for key {Key}: {Version}", key, entry.Version);
            return Task.FromResult(entry.Version);
        }
        
        _logger.LogDebug("No version found for key {Key}, returning -1", key);
        return Task.FromResult(-1L);
    }
    
    /// <inheritdoc />
    public Task<List<T>> FindStatesByPatternAsync<T>(
        string pattern, 
        Func<T, bool>? predicate = null, 
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (string.IsNullOrEmpty(pattern))
            throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
            
        try
        {
            // تبدیل الگوی وایلدکارد به الگوی منظم
            var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
            
            var result = new List<T>();
            
            // پیمایش همه کلیدها و یافتن موارد منطبق
            foreach (var entry in _states)
            {
                if (!regex.IsMatch(entry.Key) || entry.Value.Json == null)
                    continue;
                    
                try
                {
                    var state = JsonSerializer.Deserialize<T>(entry.Value.Json, _jsonOptions);
                    if (state != null && (predicate == null || predicate(state)))
                    {
                        result.Add(state);
                    }
                }
                catch (JsonException)
                {
                    // نادیده گرفتن حالت‌هایی که با نوع T منطبق نیستند
                    _logger.LogDebug("Could not deserialize state for key {Key} to type {Type}", 
                        entry.Key, typeof(T).Name);
                }
            }
            
            _logger.LogDebug("Found {Count} states matching pattern {Pattern}", result.Count, pattern);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding states with pattern {Pattern}", pattern);
            throw new StateStoreException($"Error finding states with pattern {pattern}", ex);
        }
    }

    /// <summary>
    /// تعداد وضعیت‌های ذخیره شده
    /// </summary>
    public int Count => _states.Count;
    
    /// <summary>
    /// پاک کردن همه وضعیت‌ها
    /// </summary>
    public void Clear()
    {
        _states.Clear();
        _logger.LogInformation("Cleared all states");
    }
}

/// <summary>
/// استثنای مخزن وضعیت
/// </summary>
public class StateStoreException : Exception
{
    /// <summary>
    /// ایجاد یک نمونه جدید از استثنای مخزن وضعیت
    /// </summary>
    /// <param name="message">پیام خطا</param>
    /// <param name="innerException">استثنای داخلی</param>
    public StateStoreException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// استثنای همروندی
/// </summary>
public class ConcurrencyException : Exception
{
    /// <summary>
    /// ایجاد یک نمونه جدید از استثنای همروندی
    /// </summary>
    /// <param name="message">پیام خطا</param>
    /// <param name="innerException">استثنای داخلی</param>
    public ConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
} 