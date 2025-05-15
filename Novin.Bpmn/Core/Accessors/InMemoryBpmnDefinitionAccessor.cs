using System.Collections.Concurrent;
using System.Xml.Linq;
using Novin.Bpmn.Contracts.Accessors;

namespace Novin.Bpmn.Core.Accessors
{
    /// <summary>
    /// پیاده‌سازی دسترسی به تعاریف BPMN در حافظه
    /// </summary>
    public class InMemoryBpmnDefinitionAccessor : IBpmnDefinitionAccessor
    {
        private readonly ConcurrentDictionary<string, BpmnDefinitionInfo> _definitions 
            = new ConcurrentDictionary<string, BpmnDefinitionInfo>();
        
        /// <summary>
        /// دریافت یک تعریف فرآیند با کلید
        /// </summary>
        public Task<BpmnDefinitionInfo> GetDefinitionAsync(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
                
            if (_definitions.TryGetValue(deploymentKey, out var definition))
                return Task.FromResult(definition);
                
            return Task.FromResult<BpmnDefinitionInfo>(null);
        }
        
        /// <summary>
        /// دریافت همه تعاریف فرآیندها
        /// </summary>
        public Task<IEnumerable<BpmnDefinitionInfo>> GetAllDefinitionsAsync()
        {
            return Task.FromResult<IEnumerable<BpmnDefinitionInfo>>(_definitions.Values);
        }
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و محتوا
        /// </summary>
        public Task<BpmnDefinitionInfo> DeployDefinitionAsync(string deploymentKey, string definitionXml, string label = null)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
                
            if (string.IsNullOrEmpty(definitionXml))
                throw new ArgumentException("Definition XML cannot be empty", nameof(definitionXml));
            
            // استخراج شناسه‌های فرآیندها از XML
            var processIds = ExtractProcessIds(definitionXml);
            
            var definitionInfo = new BpmnDefinitionInfo
            {
                DeploymentKey = deploymentKey,
                Label = label ?? deploymentKey,
                DefinitionXml = definitionXml,
                DeployedAt = DateTime.UtcNow,
                ProcessIds = processIds,
                RunningInstancesCount = 0
            };
            
            _definitions[deploymentKey] = definitionInfo;
            
            return Task.FromResult(definitionInfo);
        }
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و استریم محتوا
        /// </summary>
        public async Task<BpmnDefinitionInfo> DeployDefinitionAsync(string deploymentKey, Stream definitionStream, string label = null)
        {
            if (definitionStream == null)
                throw new ArgumentNullException(nameof(definitionStream));
                
            using (var reader = new StreamReader(definitionStream))
            {
                var definitionXml = await reader.ReadToEndAsync();
                return await DeployDefinitionAsync(deploymentKey, definitionXml, label);
            }
        }
        
        /// <summary>
        /// حذف یک تعریف فرآیند
        /// </summary>
        public Task<bool> DeleteDefinitionAsync(string deploymentKey)
        {
            if (string.IsNullOrEmpty(deploymentKey))
                throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
                
            return Task.FromResult(_definitions.TryRemove(deploymentKey, out _));
        }
        
        /// <summary>
        /// به‌روزرسانی تعداد نمونه‌های در حال اجرای یک فرآیند
        /// </summary>
        public void UpdateRunningInstancesCount(string deploymentKey, int count)
        {
            if (_definitions.TryGetValue(deploymentKey, out var definition))
            {
                definition.RunningInstancesCount = count;
            }
        }
        
        /// <summary>
        /// استخراج شناسه‌های فرآیندها از XML تعریف BPMN
        /// </summary>
        private List<string> ExtractProcessIds(string definitionXml)
        {
            try
            {
                var doc = XDocument.Parse(definitionXml);
                var processElements = doc.Descendants().Where(e => e.Name.LocalName == "process");
                
                var processIds = new List<string>();
                foreach (var process in processElements)
                {
                    var idAttribute = process.Attribute("id");
                    if (idAttribute != null && !string.IsNullOrEmpty(idAttribute.Value))
                    {
                        processIds.Add(idAttribute.Value);
                    }
                }
                
                return processIds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting process IDs: {ex.Message}");
                return new List<string>();
            }
        }
        
        /// <summary>
        /// دریافت یک موجودیت با کلید آن - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<BpmnDefinitionInfo> GetByIdAsync(string id)
        {
            return GetDefinitionAsync(id);
        }
        
        /// <summary>
        /// ذخیره یک موجودیت جدید یا به‌روزرسانی موجودیت موجود - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task SaveAsync(BpmnDefinitionInfo entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            
            _definitions[entity.DeploymentKey] = entity;
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// حذف یک موجودیت با کلید آن - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<bool> DeleteAsync(string id)
        {
            return DeleteDefinitionAsync(id);
        }
        
        /// <summary>
        /// دریافت تمام موجودیت‌ها - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<IEnumerable<BpmnDefinitionInfo>> GetAllAsync()
        {
            return GetAllDefinitionsAsync();
        }
        
        /// <summary>
        /// جستجو در موجودیت‌ها با استفاده از شرایط فیلتر - پیاده‌سازی IBpmnDataAccessorBase
        /// </summary>
        public Task<IEnumerable<BpmnDefinitionInfo>> FindAsync(Func<BpmnDefinitionInfo, bool> predicate)
        {
            var filteredDefinitions = _definitions.Values.Where(predicate).ToList();
            return Task.FromResult<IEnumerable<BpmnDefinitionInfo>>(filteredDefinitions);
        }
        
        /// <summary>
        /// دریافت همه تعاریف فرآیندهای مرتبط با یک کلید فرآیند خاص
        /// </summary>
        public Task<IEnumerable<BpmnDefinitionInfo>> GetDefinitionsByProcessIdAsync(string processId)
        {
            if (string.IsNullOrEmpty(processId))
                throw new ArgumentException("Process ID cannot be empty", nameof(processId));
                
            var definitions = _definitions.Values
                .Where(d => d.ProcessIds.Contains(processId))
                .ToList();
                
            return Task.FromResult<IEnumerable<BpmnDefinitionInfo>>(definitions);
        }
        
        /// <summary>
        /// به‌روز‌رسانی تعداد نمونه‌های در حال اجرای یک تعریف
        /// </summary>
        public Task UpdateRunningInstancesCountAsync(string deploymentKey, int count)
        {
            if (_definitions.TryGetValue(deploymentKey, out var definition))
            {
                definition.RunningInstancesCount = count;
                definition.LastUpdatedAt = DateTime.UtcNow;
            }
            
            return Task.CompletedTask;
        }
    }
}