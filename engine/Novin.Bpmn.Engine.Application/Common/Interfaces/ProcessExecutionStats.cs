
/// <summary>
/// Execution statistics for a process
/// </summary>
namespace Novin.Bpmn.Engine.Application.Queries
{
    public class ProcessExecutionStats
    {
        public int TotalTokens { get; set; }
        public int ActiveTokens { get; set; }
        public int CompletedTokens { get; set; }
        public int FailedTokens { get; set; }
        public int TotalNodes { get; set; }
        public int ActiveNodes { get; set; }
        public int CompletedNodes { get; set; }
        public int FailedNodes { get; set; }
    }
}
