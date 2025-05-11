using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// Records a transaction within a BPMN process
/// </summary>
public class TransactionRecord
{
    /// <summary>
    /// Unique identifier for this transaction
    /// </summary>
    public string TransactionId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Element ID that initiated the transaction
    /// </summary>
    public string InitiatingElementId { get; set; } = null!;
    
    /// <summary>
    /// Type of the initiating element
    /// </summary>
    public string InitiatingElementType { get; set; } = null!;
    
    /// <summary>
    /// Transaction type or name
    /// </summary>
    public string TransactionType { get; set; } = null!;
    
    /// <summary>
    /// Start time of the transaction
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Completion time of the transaction
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Current status of the transaction
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Active;
    
    /// <summary>
    /// List of execution IDs involved in this transaction
    /// </summary>
    public List<string> InvolvedExecutions { get; set; } = new List<string>();
    
    /// <summary>
    /// Any error information if the transaction failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of a transaction
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// Transaction is active and in progress
    /// </summary>
    Active,
    
    /// <summary>
    /// Transaction has completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Transaction has been canceled
    /// </summary>
    Canceled,
    
    /// <summary>
    /// Transaction has failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Transaction is in compensation phase
    /// </summary>
    Compensating,
    
    /// <summary>
    /// Transaction has been compensated
    /// </summary>
    Compensated
} 