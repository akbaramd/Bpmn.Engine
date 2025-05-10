using System;

namespace Novin.Bpmn.EventSourcing.Core.Deployment
{
    /// <summary>
    /// اطلاعات نصب BPMN
    /// </summary>
    public class BpmnDeploymentInfo
    {
        /// <summary>
        /// کلید نصب
        /// </summary>
        public string DeploymentKey { get; set; } = string.Empty;
        
        /// <summary>
        /// شناسه تعریف
        /// </summary>
        public string DefinitionId { get; set; } = string.Empty;
        
        /// <summary>
        /// برچسب
        /// </summary>
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// محتوای XML
        /// </summary>
        public string XmlContent { get; set; } = string.Empty;
        
        /// <summary>
        /// زمان نصب
        /// </summary>
        public DateTime DeploymentTime { get; set; }
    }
} 