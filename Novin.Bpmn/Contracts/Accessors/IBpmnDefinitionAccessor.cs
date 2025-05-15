namespace Novin.Bpmn.Contracts.Accessors
{
    /// <summary>
    /// رابط دسترسی به تعاریف فرآیندهای BPMN
    /// </summary>
    public interface IBpmnDefinitionAccessor : IBpmnDataAccessorBase<BpmnDefinitionInfo, string>
    {
        /// <summary>
        /// دریافت یک تعریف فرآیند با کلید آن
        /// </summary>
        Task<BpmnDefinitionInfo> GetDefinitionAsync(string deploymentKey);
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و محتوا
        /// </summary>
        Task<BpmnDefinitionInfo> DeployDefinitionAsync(string deploymentKey, string definitionXml, string label = null);
        
        /// <summary>
        /// بارگذاری یک فرآیند جدید با کلید و استریم محتوا
        /// </summary>
        Task<BpmnDefinitionInfo> DeployDefinitionAsync(string deploymentKey, Stream definitionStream, string label = null);
        
        /// <summary>
        /// دریافت همه تعاریف فرآیندهای مرتبط با یک کلید فرآیند خاص
        /// </summary>
        Task<IEnumerable<BpmnDefinitionInfo>> GetDefinitionsByProcessIdAsync(string processId);
        
        /// <summary>
        /// به‌روز‌رسانی تعداد نمونه‌های در حال اجرای یک تعریف
        /// </summary>
        Task UpdateRunningInstancesCountAsync(string deploymentKey, int count);
    }
    
    /// <summary>
    /// اطلاعات یک تعریف فرآیند BPMN
    /// </summary>
    public class BpmnDefinitionInfo : IBpmnEntity<string>
    {
        /// <summary>
        /// کلید دسترسی به تعریف فرآیند
        /// </summary>
        public string Id => DeploymentKey;
        
        /// <summary>
        /// کلید دسترسی به تعریف فرآیند
        /// </summary>
        public string DeploymentKey { get; set; }
        
        /// <summary>
        /// برچسب توصیفی فرآیند
        /// </summary>
        public string Label { get; set; }
        
        /// <summary>
        /// محتوای XML تعریف BPMN
        /// </summary>
        public string DefinitionXml { get; set; }
        
        /// <summary>
        /// زمان بارگذاری تعریف
        /// </summary>
        public DateTime DeployedAt { get; set; }
        
        /// <summary>
        /// زمان آخرین به‌روزرسانی تعریف
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }
        
        /// <summary>
        /// نسخه تعریف فرآیند
        /// </summary>
        public string Version { get; set; }
        
        /// <summary>
        /// لیست شناسه‌های فرآیندهای موجود در این تعریف
        /// </summary>
        public List<string> ProcessIds { get; set; } = new List<string>();
        
        /// <summary>
        /// تعداد نمونه‌های در حال اجرا
        /// </summary>
        public int RunningInstancesCount { get; set; }
    }
} 