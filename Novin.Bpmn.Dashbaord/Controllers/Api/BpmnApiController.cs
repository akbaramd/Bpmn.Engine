using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;
using Novin.Bpmn.EventSourcing.Core.Executions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Dashbaord.Models;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.Dashbaord.Controllers.Api
{
    [Route("api/bpmn")]
    [ApiController]
    public class BpmnApiController : ControllerBase
    {
        private readonly IProcessStateStore _processStateStore;
        private readonly IExecutionContextRepository _executionContextRepository;
        private readonly IExecutionPathService _executionPathService;
        private readonly IBpmnDeploymentStore _deploymentService;

        public BpmnApiController(
            IProcessStateStore processStateStore,
            IExecutionContextRepository executionContextRepository,
            IExecutionPathService executionPathService, IBpmnDeploymentStore deploymentService)
        {
            _processStateStore = processStateStore;
            _executionContextRepository = executionContextRepository;
            _executionPathService = executionPathService;
            _deploymentService = deploymentService;
        }

        /// <summary>
        /// دریافت وضعیت کلی یک instance شامل variableها و contextها
        /// </summary>
        [HttpGet("instance/{processInstanceId}")]
        public ActionResult<ProcessDetailViewModel> GetProcessInstance(Guid processInstanceId)
        {
            var state = _processStateStore.Get(processInstanceId);
            if (state == null)
                return NotFound($"Process instance '{processInstanceId}' not found.");

            // همه کانتکست‌ها
            var contexts = _executionContextRepository.GetByInstanceId(processInstanceId).ToList();

            // نقشه‌ی مسیر اجرا
            var traceMap = _executionPathService.BuildExecutionTraces(processInstanceId);

            // تبدیل به ViewModel
            var vm = new ProcessDetailViewModel
            {
                InstanceId   = state.InstanceId,
                DeploymentKey= state.DeploymentKey,
                ProcessId    = state.ProcessId,
                Status       = state.Status.ToString(),
                StartTime    = state.CreatedAt,
                EndTime      = (state.Status == ProcessStateStatus.Completed ||
                                state.Status == ProcessStateStatus.Terminated)
                    ? state.LastUpdatedAt : null,
                Traces            = traceMap.Traces,              // همان ExecutionTrace‌های موجود
                ExecutionContexts = contexts,
                CurrentElementByContextId = contexts.ToDictionary(
                    c => c.ContextId,
                    c => c.CurrentElementId ?? string.Empty),
                Variables = state.Variables ?? new Dictionary<string, object?>()
            };

            return Ok(vm);
        }


        /// <summary>
        /// دریافت ExecutionTraceMap برای نمایش مسیر اجرای فرآیند
        /// </summary>
        [HttpGet("execution-map/{processInstanceId}")]
        public ActionResult<ExecutionTraceMap> GetExecutionMap(Guid processInstanceId)
        {
            var instance = _processStateStore.Get(processInstanceId);
            if (instance == null)
                return NotFound($"Process instance with ID {processInstanceId} not found.");

            var traceMap = _executionPathService.BuildExecutionTraces(processInstanceId);
            return Ok(traceMap);
        }

        /// <summary>
        /// دریافت فقط contextهایی که هنوز فعال‌اند
        /// </summary>
        [HttpGet("active-contexts/{processInstanceId}")]
        public ActionResult<List<ExecutionContext>> GetActiveContexts(Guid processInstanceId)
        {
            var active = _executionContextRepository
                .GetByInstanceId(processInstanceId)
                .Where(ctx => ctx.State == ExecutionState.Active)
                .ToList();

            return Ok(active);
        }
        // ----------------------------------------------------------------
        /// <summary>
        ///  برگرداندن XML کامل فرآیند برای یک Instance
        /// </summary>
        [HttpGet("content/process/{processInstanceId}")]
        public ActionResult GetProcessXml(Guid processInstanceId)
        {
            var state = _processStateStore.Get(processInstanceId);
            if (state == null)
                return NotFound($"Process instance '{processInstanceId}' not found.");

            var deployment = _deploymentService.GetById(state.DeploymentId);
            if (deployment == null)
                return NotFound($"Deployment '{state.DeploymentId}' not found.");

            // XML BPMN که هنگام Deploy ذخیره شده
            return Content(deployment.XmlContent, "application/xml");
        }
        /// <summary>
        /// دریافت لیست کلیه processهای موجود در حافظه
        /// </summary>
        [HttpGet("all")]
        public ActionResult<IEnumerable<object>> GetAllProcesses()
        {
            var processes = _processStateStore.GetAll();

            return Ok(processes.Select(p => new
            {
                p.InstanceId,
                p.DeploymentKey,
                p.ProcessId,
                p.Status,
                p.CreatedAt,
                p.LastUpdatedAt
            }));
        }
    }
}
