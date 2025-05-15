using System.Collections.Concurrent;
using Novin.Bpmn.Contracts.Accessors;
using Novin.Bpmn.V3;

namespace Novin.Bpmn.Core.Accessors
{
    /// <summary>
    /// پیاده‌سازی دسترسی به نمونه‌های فرآیند BPMN در حافظه
    /// </summary>
    public class InMemoryBpmnProcessInstanceAccessor : IBpmnProcessInstanceAccessor
    {
        // ذخیره نمونه‌های فرآیند با کلید شناسه آنها
        private readonly ConcurrentDictionary<string, BpmnV3ProcessInstance> _instances 
            = new ConcurrentDictionary<string, BpmnV3ProcessInstance>();
            
        // نگاشت شناسه توکن به شناسه نمونه فرآیند
        private readonly ConcurrentDictionary<Guid, string> _tokenToInstanceMap
            = new ConcurrentDictionary<Guid, string>();
            
        // متادیتای نمونه فرآیندها برای جستجو
        private readonly ConcurrentDictionary<string, ProcessInstanceMetadata> _metadata
            = new ConcurrentDictionary<string, ProcessInstanceMetadata>();
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        public Task<BpmnV3ProcessInstance> GetInstanceAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                throw new ArgumentException("Instance ID cannot be empty", nameof(instanceId));
                
            if (_instances.TryGetValue(instanceId, out var instance))
                return Task.FromResult(instance);
                
            return Task.FromResult<BpmnV3ProcessInstance>(null);
        }
        
        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه توکن
        /// </summary>
        public Task<BpmnV3ProcessInstance> GetInstanceByTokenAsync(Guid tokenId)
        {
            if (_tokenToInstanceMap.TryGetValue(tokenId, out var instanceId))
            {
                return GetInstanceAsync(instanceId);
            }
            
            // جستجو در تمام نمونه‌های موجود
            foreach (var instance in _instances.Values)
            {
                if (instance.Tokens.Any(t => t.Id == tokenId))
                {
                    // ذخیره نگاشت برای دفعات بعد
                    _tokenToInstanceMap[tokenId] = instance.Id;
                    return Task.FromResult(instance);
                }
            }
            
            return Task.FromResult<BpmnV3ProcessInstance>(null);
        }
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای در حال اجرا
        /// </summary>
        public Task<IEnumerable<BpmnV3ProcessInstance>> GetAllActiveInstancesAsync()
        {
            var activeInstanceIds = _metadata.Values
                .Where(m => m.Status == ProcessInstanceStatus.Active)
                .Select(m => m.InstanceId);
                
            var activeInstances = activeInstanceIds
                .Select(id => _instances[id])
                .ToList();
                
            return Task.FromResult<IEnumerable<BpmnV3ProcessInstance>>(activeInstances);
        }
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک تعریف خاص
        /// </summary>
        public Task<IEnumerable<BpmnV3ProcessInstance>> GetInstancesByDeploymentKeyAsync(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
                
            var instanceIds = _metadata.Values
                .Where(m => m.DeploymentKey == deploymentKey)
                .Select(m => m.InstanceId);
                
            var instances = instanceIds
                .Select(id => _instances[id])
                .ToList();
                
            return Task.FromResult<IEnumerable<BpmnV3ProcessInstance>>(instances);
        }
        
        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای یک فرآیند خاص
        /// </summary>
        public Task<IEnumerable<BpmnV3ProcessInstance>> GetInstancesByProcessIdAsync(string processId)
        {
            if (string.IsNullOrEmpty(processId))
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));
                
            var instanceIds = _metadata.Values
                .Where(m => m.ProcessId == processId)
                .Select(m => m.InstanceId);
                
            var instances = instanceIds
                .Select(id => _instances[id])
                .ToList();
                
            return Task.FromResult<IEnumerable<BpmnV3ProcessInstance>>(instances);
        }
        
        /// <summary>
        /// ذخیره یک نمونه فرآیند
        /// </summary>
        public Task SaveInstanceAsync(BpmnV3ProcessInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
                
            // ذخیره نمونه فرآیند
            _instances[instance.Id] = instance;
            
            // به‌روزرسانی یا ایجاد متادیتا
            if (!_metadata.TryGetValue(instance.Id, out var metadata))
            {
                metadata = new ProcessInstanceMetadata
                {
                    InstanceId = instance.Id,
                    ProcessId = instance.ProcessElementId,
                    DeploymentKey = instance.DeploymentKey ?? "unknown",
                    StartedAt = DateTime.UtcNow,
                    Status = ProcessInstanceStatus.Active
                };
                
                _metadata[instance.Id] = metadata;
            }
            
            // به‌روزرسانی نگاشت توکن‌ها
            foreach (var token in instance.Tokens)
            {
                _tokenToInstanceMap[token.Id] = instance.Id;
            }
            
            // به‌روزرسانی زمان آخرین تغییر
            metadata.LastUpdatedAt = DateTime.UtcNow;
            
            // تشخیص وضعیت نمونه فرآیند
            UpdateInstanceStatus(instance, metadata);
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// به‌روزرسانی وضعیت یک نمونه فرآیند
        /// </summary>
        public async Task<bool> UpdateInstanceStatusAsync(string instanceId, ProcessInstanceStatus status)
        {
            if (string.IsNullOrEmpty(instanceId))
                throw new ArgumentException("Instance ID cannot be empty", nameof(instanceId));
                
            if (_metadata.TryGetValue(instanceId, out var metadata))
            {
                metadata.Status = status;
                metadata.LastUpdatedAt = DateTime.UtcNow;
                
                if (status == ProcessInstanceStatus.Completed)
                {
                    metadata.CompletedAt = DateTime.UtcNow;
                }
            }
            
            return true;  
        }
        
        /// <summary>
        /// حذف یک نمونه فرآیند
        /// </summary>
        public Task<bool> DeleteInstanceAsync(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                throw new ArgumentException("Instance ID cannot be empty", nameof(instanceId));
                
            // حذف نمونه فرآیند
            bool instanceRemoved = _instances.TryRemove(instanceId, out var instance);
            
            if (instanceRemoved && instance != null)
            {
                // حذف توکن‌های مرتبط
                foreach (var token in instance.Tokens)
                {
                    _tokenToInstanceMap.TryRemove(token.Id, out _);
                }
                
                // حذف متادیتا
                _metadata.TryRemove(instanceId, out _);
            }
            
            return Task.FromResult(instanceRemoved);
        }
        
        /// <summary>
        /// تشخیص وضعیت نمونه فرآیند بر اساس وضعیت توکن‌ها
        /// </summary>
        private void UpdateInstanceStatus(BpmnV3ProcessInstance instance, ProcessInstanceMetadata metadata)
        {
            // محاسبه وضعیت بر اساس توکن‌ها
            bool hasActiveTokens = instance.Tokens.Any(t => t.Status == TokenStatus.Active);
            bool hasWaitingTokens = instance.Tokens.Any(t => t.Status == TokenStatus.Waiting);
            bool hasAnyTokens = instance.Tokens.Any();
            
            if (!hasAnyTokens)
            {
                metadata.Status = ProcessInstanceStatus.Error;
            }
            else if (hasActiveTokens)
            {
                metadata.Status = ProcessInstanceStatus.Active;
            }
            else if (hasWaitingTokens)
            {
                metadata.Status = ProcessInstanceStatus.Active; // همچنان فعال است، در حال انتظار برای اقدام کاربر
            }
            else
            {
                metadata.Status = ProcessInstanceStatus.Completed;
                metadata.CompletedAt = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// دریافت یک موجودیت با کلید آن - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<BpmnV3ProcessInstance> GetByIdAsync(string id)
        {
            return GetInstanceAsync(id);
        }
        
        /// <summary>
        /// ذخیره یک موجودیت جدید یا به‌روزرسانی موجودیت موجود - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task SaveAsync(BpmnV3ProcessInstance entity)
        {
            return SaveInstanceAsync(entity);
        }
        
        /// <summary>
        /// حذف یک موجودیت با کلید آن - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<bool> DeleteAsync(string id)
        {
            return DeleteInstanceAsync(id);
        }
        
        /// <summary>
        /// دریافت تمام موجودیت‌ها - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<IEnumerable<BpmnV3ProcessInstance>> GetAllAsync()
        {
            return Task.FromResult<IEnumerable<BpmnV3ProcessInstance>>(_instances.Values.ToList());
        }
        
        /// <summary>
        /// جستجو در موجودیت‌ها با استفاده از شرایط فیلتر - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<IEnumerable<BpmnV3ProcessInstance>> FindAsync(Func<BpmnV3ProcessInstance, bool> predicate)
        {
            var filteredInstances = _instances.Values.Where(predicate).ToList();
            return Task.FromResult<IEnumerable<BpmnV3ProcessInstance>>(filteredInstances);
        }
    }
    
    /// <summary>
    /// متادیتای یک نمونه فرآیند
    /// </summary>
    internal class ProcessInstanceMetadata
    {
        public string InstanceId { get; set; }
        public string ProcessId { get; set; }
        public string DeploymentKey { get; set; }
        public ProcessInstanceStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
} 