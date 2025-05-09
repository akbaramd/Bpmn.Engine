# Dual Token Strategy Implementation in BPMN Engine V3

This document describes the dual token strategy implementation for handling gateways in the BPMN Engine V3.

## Overview

The dual token strategy uses both executable and non-executable tokens to simplify gateway merge logic and improve process visualization. This approach makes it easier to handle all types of gateways (Exclusive, Inclusive, Parallel) consistently.

## Token Types

1. **Executable Tokens** (`IsExecutable = true`)
   - Execute actual activities (script tasks, service tasks, etc.)
   - Evaluate conditions in gateways
   - Trigger actual process logic

2. **Non-Executable Tokens** (`IsExecutable = false`)
   - Visualize inactive paths in the process
   - Complete merge logic in gateways
   - Don't execute activities or evaluate conditions

## Gateway Behavior

### Exclusive Gateway (XOR)

- **Split**: Only one path gets an executable token (condition evaluates to true)
- **Join**: First token arriving can continue, others are consumed
- **Implementation**: All paths receive tokens, but only one gets `IsExecutable = true`

### Inclusive Gateway (OR)

- **Split**: Multiple paths can get executable tokens if their conditions evaluate to true
- **Join**: Waits for tokens from all active paths before continuing
- **Implementation**: All paths receive tokens, paths with true conditions get `IsExecutable = true`

### Parallel Gateway (AND)

- **Split**: All paths get tokens with the same executability status
- **Join**: Waits for tokens from all paths before continuing
- **Implementation**: All paths receive tokens with same `IsExecutable` value

## Benefits

1. **Simplified Merge Logic**: By sending tokens (executable or non-executable) on all paths, the merge logic simply counts tokens rather than trying to analyze which paths were taken
2. **Improved Visualization**: All paths in the process can be visualized, even those not executed
3. **Consistent Gateway Handling**: The same pattern works for all gateway types
4. **Easier Timer/Boundary Event Handling**: Simplified logic for tracking which activities are active

## Implementation Classes

The main components of this implementation are:

- `BpmnV3Token`: Represents a token with executability status
- `BpmnV3BaseGatewayHandler`: Base class with common gateway handling logic
- `BpmnV3ExclusiveGatewayHandler`: Exclusive gateway implementation
- `BpmnV3InclusiveGatewayHandler`: Inclusive gateway implementation
- `BpmnV3ParallelGatewayHandler`: Parallel gateway implementation
- `BpmnV3GatewayRouter`: Routes tokens to the appropriate handler

## Merge Logic Example

Instead of complex path analysis for merging, the dual token approach simplifies the logic:

```csharp
// In the base gateway handler
public virtual bool CanMerge(BpmnGateway gateway, BpmnV3ProcessInstance processInstance)
{
    // Get tokens waiting at this gateway
    var tokensAtGateway = processInstance.Tokens
        .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
        .ToList();

    // Get the number of incoming flows
    var incomingFlows = processInstance.DefinitionsHandler.GetIncomingSequenceFlows(gateway);
    
    // Default implementation: gateway can merge when all incoming paths have tokens
    return tokensAtGateway.Count >= incomingFlows.Count;
}
``` 