using Novin.Bpmn;
using Novin.Bpmn.Models;

public class BpmnV3ProcessExecutor
{
    private readonly BpmnV3ProcessInstance _processInstance;

    public BpmnV3ProcessExecutor(BpmnV3ProcessInstance processInstance)
    {
        _processInstance = processInstance;

        if (!_processInstance.Tokens.Any())
        {
            var startEvent = FindStartEvent();
            if (startEvent == null)
            {
                throw new InvalidOperationException("No start event found in the process definition.");
            }

            // Create the first token at the start event
            var startEventId = startEvent.id;
            _processInstance.CreateToken(startEventId);
        }
    }

    // Start the execution process
    public async Task StartProcessAsync()
    {
        

        // Process tokens in a loop
        try
        {
            while (_processInstance.Tokens.Any(t => t.Status == TokenStatus.Active ))
            {
                // Handle active tokens
                foreach (var token in _processInstance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList())
                {
                    if (token.IsExecutable)
                    {
                        Console.WriteLine($"Processing token at element {token.Id} {token.CurrentElementId} : {token.IsExecutable}");
                    }
                    await _processInstance.MoveToken(token);
                }

              
            }

            Console.WriteLine("Process execution completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during process execution: {ex.Message}");
            throw;
        }
    }

    // Complete a user task and continue the process
    public async Task CompleteUserTaskAsync(Guid tokenId)
    {
        var token = _processInstance.Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token == null)
        {
            throw new InvalidOperationException($"Token with ID {tokenId} not found.");
        }

        if (token.Status != TokenStatus.Waiting)
        {
            throw new InvalidOperationException($"Token {tokenId} is not in a waiting state.");
        }

        // Reactivate the token and continue the process
        token.Reactivate();
        
        Console.WriteLine($"User task completed for token {token.Id}. Reactivating the token.");

        // Continue processing the reactivated token
        await StartProcessAsync();
    }

    // Finds the first start event in the process definition
    private BpmnFlowElement FindStartEvent()
    {
        return _processInstance.DefinitionsHandler.GetStartEventsForProcess(_processInstance.ProcessElementId).FirstOrDefault();
    }
}
