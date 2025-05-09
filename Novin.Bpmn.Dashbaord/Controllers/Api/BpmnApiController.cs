using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;
using Novin.Bpmn.V3;
using System;
using System.Threading.Tasks;

namespace Novin.Bpmn.Dashbaord.Controllers.Api;

[Route("api/bpmn")]
public class BpmnApiController : ControllerBase
{
    private readonly IBpmnDefinitionAccessor _definitionAccessor;
    private readonly IBpmnProcessInstanceAccessor _processInstanceAccessor;
    private readonly IBpmnProcessManager _processManager;

    public BpmnApiController(
        IBpmnDefinitionAccessor definitionAccessor,
        IBpmnProcessInstanceAccessor processInstanceAccessor,
        IBpmnProcessManager processManager)
    {
        _definitionAccessor = definitionAccessor;
        _processInstanceAccessor = processInstanceAccessor;
        _processManager = processManager;
    }
    
    [HttpGet("content/{deploymentKey}")]
    public async Task<ActionResult<string>> Content(string deploymentKey)
    {
        try
        {
            var definition = await _definitionAccessor.GetDefinitionAsync(deploymentKey);
            if (definition == null)
            {
                return NotFound($"Definition with key {deploymentKey} not found");
            }
            return Ok(definition.DefinitionXml);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving definition: {ex.Message}");
        }
    }
    
    [HttpGet("content/process/{processId}")]
    public async Task<ActionResult> ProcessContent(Guid processId)
    {
        try
        {
            var instanceId = processId.ToString();
            var instance = await _processInstanceAccessor.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Process with ID {processId} not found");
            }
            
            // Return XML content with the appropriate content type
            return Content(instance.DefinitionXml, "application/xml");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving process: {ex.Message}");
        }
    }
    
    [HttpGet("execution-map/{processId}")]
    public async Task<ActionResult<ProcessExecutionMap>> GetExecutionMap(Guid processId, [FromQuery] bool includeVirtual = true)
    {
        try
        {
            var instanceId = processId.ToString();
            var instance = await _processInstanceAccessor.GetInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Process with ID {processId} not found");
            }
            
            // Get process manager for this instance
            var executor = await _processManager.GetExecutorForInstanceAsync(instanceId);
            
            // Get execution map
            var executionMap = executor.GetExecutionMap(includeVirtual);
            return Ok(executionMap);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error getting execution map: {ex.Message}");
        }
    }
    
    [HttpPost("save")]
    public async Task<ActionResult> SaveDiagram([FromBody] SaveDiagramRequest request)
    {
        try
        {
            var definition = await _definitionAccessor.GetDefinitionAsync(request.DefinitionKey);
            if (definition == null)
            {
                return NotFound($"Definition with key {request.DefinitionKey} not found");
            }
            
            // Update the definition XML
            definition.DefinitionXml = request.BpmnXML;
            
            // Save the updated definition
            await _definitionAccessor.SaveAsync(definition);
            
            return Ok(new { success = true, message = "BPMN diagram saved successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"Error saving diagram: {ex.Message}" });
        }
    }
}

public class SaveDiagramRequest
{
    public string DefinitionKey { get; set; }
    public string BpmnXML { get; set; }
}