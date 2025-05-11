using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// اطلاعات ادغام دروازه‌ها
/// Tracks information for gateway merges
/// </summary>
public class GatewayMergeInfo
{
    /// <summary>
    /// شناسه دروازه
    /// Gateway ID
    /// </summary>
    public string GatewayId { get; set; } = null!;
    
    /// <summary>
    /// نوع دروازه
    /// Gateway type
    /// </summary>
    public string GatewayType { get; set; } = null!;
    
    /// <summary>
    /// تعداد جریان‌های ورودی مورد نیاز
    /// Number of incoming flows required for merge
    /// </summary>
    public int RequiredIncomingFlows { get; set; }
    
    /// <summary>
    /// تعداد جریان‌های ورودی دریافت شده
    /// Number of incoming flows received so far
    /// </summary>
    public int ReceivedIncomingFlows => ReceivedFlowIds.Count;
    
    /// <summary>
    /// شناسه جریان‌های ورودی
    /// IDs of all incoming flows
    /// </summary>
    public List<string> IncomingFlowIds { get; set; } = new();
    
    /// <summary>
    /// شناسه جریان‌های ورودی دریافت شده
    /// IDs of received incoming flows
    /// </summary>
    public List<string> ReceivedFlowIds { get; set; } = new();
    
    /// <summary>
    /// تاریخ اولین دریافت
    /// Time of first received flow
    /// </summary>
    public DateTime FirstFlowReceivedAt { get; set; } = DateTime.MinValue;
    
    /// <summary>
    /// تاریخ آخرین دریافت
    /// Time of last received flow
    /// </summary>
    public DateTime LastFlowReceivedAt { get; set; } = DateTime.MinValue;
    
    /// <summary>
    /// آیا شرایط ادغام برقرار است
    /// Whether merge conditions are satisfied
    /// </summary>
    public bool CanMerge => GatewayType switch
    {
        "bpmn:ParallelGateway" => ReceivedIncomingFlows >= RequiredIncomingFlows,
        "bpmn:InclusiveGateway" => ReceivedIncomingFlows >= RequiredIncomingFlows,
        "bpmn:ExclusiveGateway" => ReceivedIncomingFlows >= 1,
        _ => ReceivedIncomingFlows >= 1
    };
    
    /// <summary>
    /// آیا دروازه در حال انتظار است
    /// Whether the gateway is waiting for more incoming flows
    /// </summary>
    public bool IsWaiting => !CanMerge;
    
    /// <summary>
    /// ثبت دریافت یک جریان ورودی
    /// Record the receipt of an incoming flow
    /// </summary>
    /// <param name="flowId">Flow ID</param>
    /// <returns>Whether the gateway can now merge</returns>
    public bool RecordFlowReceived(string flowId)
    {
        if (ReceivedFlowIds.Contains(flowId))
        {
            return CanMerge; // Already received this flow
        }
        
        ReceivedFlowIds.Add(flowId);
        
        var now = DateTime.UtcNow;
        
        if (FirstFlowReceivedAt == DateTime.MinValue)
        {
            FirstFlowReceivedAt = now;
        }
        
        LastFlowReceivedAt = now;
        
        return CanMerge;
    }
    
    /// <summary>
    /// Valid outgoing flow IDs (for split gateways)
    /// </summary>
    public List<string> ValidOutgoingFlowIds { get; set; } = new List<string>();
    
    /// <summary>
    /// Invalid outgoing flow IDs (for split gateways)
    /// </summary>
    public List<string> InvalidOutgoingFlowIds { get; set; } = new List<string>();
    
    /// <summary>
    /// Track which flows are executable
    /// </summary>
    public Dictionary<string, bool> ExecutableFlows { get; set; } = new Dictionary<string, bool>();
    
    /// <summary>
    /// Record executable status of an incoming flow
    /// </summary>
    public void RecordFlowExecutableStatus(string flowId, bool isExecutable)
    {
        if (!string.IsNullOrEmpty(flowId))
        {
            ExecutableFlows[flowId] = isExecutable;
        }
    }
} 