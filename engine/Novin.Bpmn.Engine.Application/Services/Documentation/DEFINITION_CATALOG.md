# BPMN Definition Catalog - Memory Store Architecture

## ✅ پیاده‌سازی کامل

### 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│              Runtime (Handlers/Gateways)                 │
│  فقط از IExecutableDefinitionCatalog استفاده می‌کند    │
└────────────────────┬────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│         IExecutableDefinitionCatalog                    │
│  Strategy: Memory → DB → XML (compile on-the-fly)       │
└────────────────────┬────────────────────────────────────┘
                      │
        ┌─────────────┴─────────────┐
        ▼                           ▼
┌──────────────────┐      ┌──────────────────────┐
│ Memory Store      │      │ Deployment Repository │
│ (Singleton)       │      │ (DB)                  │
│ Thread-safe       │      │                       │
└──────────────────┘      └──────────────────────┘
```

### 2. Components

#### 2.1 ProcessDefinitionRef
```csharp
var defRef = new ProcessDefinitionRef(
    deploymentId: deployment.Id,
    processBpmnId: "my-process",
    version: deployment.Version);

// Cache key: "{DeploymentId}:{ProcessBpmnId}:{Version}"
```

#### 2.2 IBpmnDefinitionMemoryStore
- **Singleton**: یک instance برای کل application
- **Thread-safe**: ConcurrentDictionary
- **Fast lookup**: O(1) access

#### 2.3 IExecutableDefinitionCatalog
- **GetAsync**: Memory-first lookup
- **WarmUpAllAsync**: Startup warm-up
- **OnDefinitionChangedAsync**: Invalidate + reload

### 3. Startup Warm-up

```csharp
// DefinitionWarmupHostedService automatically runs on startup
// Loads all active deployments and compiles them into memory
```

**Performance:**
- Batch size: 200 definitions
- Max parallelism: 4 concurrent compilations
- Logs progress and errors

### 4. Deploy/Update Flow

#### 4.1 Create Deployment
```
POST /api/deployments
  ↓
CreateDeploymentCommand
  ↓
Deployment.Create() → DeploymentCreatedEvent
  ↓
Transaction Commit → Outbox
  ↓
DeploymentCreatedEventHandler → Warm up in memory
```

#### 4.2 Update Deployment (Versioning)
```
PUT /api/deployments/{id}?RequestedVersion=2
  ↓
UpdateDeploymentCommand
  ↓
if (RequestedVersion > CurrentVersion && BpmnXml provided)
  → CreateNextVersion() → New DeploymentCreatedEvent
  → Old Deployment.Deactivate() → DeploymentUpdatedEvent
  ↓
Transaction Commit → Outbox
  ↓
DeploymentCreatedEventHandler → Warm up NEW version
DeploymentUpdatedEventHandler → Invalidate OLD version
```

**Versioning Rules:**
- اگر `RequestedVersion > CurrentVersion` → ایجاد version جدید
- Version قدیم به صورت خودکار deactivate می‌شود
- هر version یک DeploymentId جداگانه دارد

### 5. Usage in Handlers

**❌ WRONG - Don't do this:**
```csharp
// DON'T parse XML directly
var deployment = await _deploymentRepository.GetByIdAsync(process.DeploymentId, ct);
var definitions = deployment.GetDefinitions(); // ❌

// DON'T query DB for definitions
var bpmnProcess = _bpmnQuery.GetProcessOrThrow(deployment, process.ProcessBpmnId); // ❌
```

**✅ CORRECT - Do this:**
```csharp
// ✅ Use catalog (memory-first)
var defRef = ProcessDefinitionRef.From(process, deployment);
var compiled = await _catalog.GetAsync(defRef, ct);

// Use compiled definition
var bpmnProcess = compiled.Process;
var allElements = compiled.Definitions.Items;
```

### 6. Cache Invalidation

#### 6.1 When Deployment is Updated
- `DeploymentUpdatedEvent` → `DeploymentUpdatedEventHandler`
- Invalidates specific deployment (by ID, not by key)
- Reloads from DB and recompiles

#### 6.2 When New Version is Created
- `DeploymentCreatedEvent` (for new version) → Warm up
- `DeploymentUpdatedEvent` (for old version) → Invalidate

#### 6.3 When Deployment is Deactivated
- Currently: Cache remains (can be extended to invalidate)

### 7. Performance Indexes

#### 7.1 Token Indexes
```sql
-- Hot Query: GetTokensForJoin
IX_Token_Scope_Element_State (ScopeId, CurrentElementId, State)

-- Hot Query: GetActiveTokens
IX_Token_Process_State (ProcessId, State)

-- Hot Query: Trace/Visualization
IX_Token_Process_Element_State (ProcessId, CurrentElementId, State)

-- Hot Query: GetChildren
IX_Token_ParentTokenId (ParentTokenId)
```

#### 7.2 NodeInstance Indexes
```sql
-- Hot Query: Dashboard
IX_NodeInstance_Process_State (ProcessId, State)
IX_NodeInstance_Process_State_Created (ProcessId, State, CreatedAtUtc)

-- Hot Query: Trace/Visualization
IX_NodeInstance_Process_Element (ProcessId, ElementId)
IX_NodeInstance_Process_Element_Created (ProcessId, ElementId, CreatedAtUtc)

-- Hot Query: GetByTokenId
IX_NodeInstance_TokenId (TokenId)
IX_NodeInstance_TokenId_State (TokenId, State)

-- Hot Query: Tasklist
IX_NodeInstance_WorkerId_State (WorkerId, State)
```

### 8. Example: Using Catalog in Handler

```csharp
public class SomeBpmnHandler
{
    private readonly IExecutableDefinitionCatalog _catalog;
    private readonly IDeploymentRepository _deploymentRepository;

    public async Task Handle(SomeEvent e, CancellationToken ct)
    {
        // Get process and deployment
        var process = await _processRepository.GetByIdAsync(e.ProcessId, ct);
        var deployment = await _deploymentRepository.GetByIdAsync(process.DeploymentId, ct);

        // ✅ Get compiled definition from memory
        var defRef = ProcessDefinitionRef.From(process, deployment);
        var compiled = await _catalog.GetAsync(defRef, ct);

        // Use compiled definition
        var bpmnProcess = compiled.Process;
        var element = _bpmnQuery.GetElementOrThrow<BpmnUserTask>(
            compiled.Definitions, 
            compiled.Process.id, 
            "UserTask_1");

        // ... use element ...
    }
}
```

### 9. Monitoring

```csharp
// Check memory store count
var count = _memoryStore.Count; // Number of definitions in memory

// Logs:
// - "Warming up BPMN definitions..."
// - "Warm-up complete. {Count} definitions in memory"
// - "Definition compiled and cached: {Ref}"
// - "Invalidated and reloaded definition: {Ref}"
```

### 10. Best Practices

1. **Always use IExecutableDefinitionCatalog** - Never parse XML directly
2. **ProcessDefinitionRef.From()** - Use helper method to create refs
3. **Versioning** - Use `RequestedVersion` parameter for explicit versioning
4. **Cache invalidation** - Automatic via events (Outbox pattern)
5. **Startup warm-up** - Automatic via `DefinitionWarmupHostedService`

### 11. Troubleshooting

**Problem**: Definition not found in memory
- **Solution**: Check if deployment is active
- **Solution**: Check if warm-up completed successfully
- **Solution**: Check logs for compilation errors

**Problem**: Stale definition in memory
- **Solution**: Check if `DeploymentUpdatedEvent` was published
- **Solution**: Check if `DeploymentUpdatedEventHandler` ran successfully
- **Solution**: Manual invalidation: `await _catalog.OnDefinitionChangedAsync(defRef, ct)`

