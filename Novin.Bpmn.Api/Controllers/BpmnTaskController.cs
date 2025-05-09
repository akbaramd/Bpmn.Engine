using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.UserTasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.Api.Controllers
{
    [ApiController]
    [Route("api/bpmn/tasks")]
    public class BpmnTaskController : ControllerBase
    {
        private readonly BpmnV3UserTaskManager _userTaskManager;
        private readonly IBpmnProcessRepository _processRepository;
        
        public BpmnTaskController(
            BpmnV3UserTaskManager userTaskManager,
            IBpmnProcessRepository processRepository)
        {
            _userTaskManager = userTaskManager;
            _processRepository = processRepository;
        }
        
        /// <summary>
        /// دریافت وظایف کاربری مربوط به کاربر فعلی
        /// </summary>
        [HttpGet("my-tasks")]
        public IActionResult GetMyTasks()
        {
            // در اینجا می‌توان شناسه کاربر را از کلایم‌های احراز هویت گرفت
            string userId = User.Identity.Name ?? "demo-user";
            
            // گروه‌های کاربر را می‌توان از سیستم مدیریت کاربران دریافت کرد
            var userGroups = new List<string> { "managers", "users" };
            
            var tasks = _userTaskManager.GetUserTasks(userId, userGroups);
            
            return Ok(tasks);
        }
        
        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند خاص
        /// </summary>
        [HttpGet("process/{processInstanceId}")]
        public async Task<IActionResult> GetProcessTasks(string processInstanceId)
        {
            var process = await _processRepository.GetProcessInstanceAsync(processInstanceId);
            if (process == null)
            {
                return NotFound($"Process instance {processInstanceId} not found");
            }
            
            var executor = new BpmnV3ProcessExecutor(process, userTaskManager: _userTaskManager);
            var tasks = executor.GetAllUserTasks();
            
            return Ok(tasks);
        }
        
        /// <summary>
        /// تخصیص یک وظیفه کاربری به کاربر
        /// </summary>
        [HttpPost("{tokenId}/claim")]
        public async Task<IActionResult> ClaimTask(Guid tokenId)
        {
            try
            {
                // دریافت شناسه کاربر از کلایم‌های احراز هویت
                string userId = User.Identity.Name ?? "demo-user";
                
                // یافتن وظیفه کاربری مرتبط با این توکن
                var task = _userTaskManager.GetTaskByTokenId(tokenId);
                if (task == null)
                {
                    return NotFound($"Task with token {tokenId} not found");
                }
                
                // تخصیص وظیفه به کاربر
                var claimedTask = _userTaskManager.ClaimUserTask(tokenId, userId);
                
                return Ok(claimedTask);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        /// <summary>
        /// تکمیل یک وظیفه کاربری
        /// </summary>
        [HttpPost("{tokenId}/complete")]
        public async Task<IActionResult> CompleteTask(Guid tokenId, [FromBody] Dictionary<string, object> formData)
        {
            try
            {
                // دریافت شناسه کاربر از کلایم‌های احراز هویت
                string userId = User.Identity.Name ?? "demo-user";
                
                // یافتن فرآیند مرتبط با این توکن
                var task = _userTaskManager.GetTaskByTokenId(tokenId);
                if (task == null)
                {
                    return NotFound($"Task with token {tokenId} not found");
                }
                
                // یافتن نمونه فرآیند
                var processInstance = await _processRepository.GetProcessInstanceByTokenAsync(tokenId);
                if (processInstance == null)
                {
                    return NotFound($"Process instance for token {tokenId} not found");
                }
                
                // ساخت یک پردازنده برای این فرآیند
                var executor = new BpmnV3ProcessExecutor(processInstance, userTaskManager: _userTaskManager);
                
                // تکمیل وظیفه و ادامه اجرای فرآیند
                var updatedProcess = await executor.CompleteUserTaskAsync(tokenId, userId, formData);
                
                // ذخیره وضعیت به‌روز شده فرآیند
                await _processRepository.SaveProcessInstanceAsync(updatedProcess);
                
                return Ok(new
                {
                    Message = "Task completed successfully",
                    ProcessInstanceId = updatedProcess.Id,
                    CurrentStatus = executor.GetProcessStatus()
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
    
    /// <summary>
    /// رابط مخزن فرآیندها
    /// </summary>
    public interface IBpmnProcessRepository
    {
        Task<BpmnV3ProcessInstance> GetProcessInstanceAsync(string processInstanceId);
        Task<BpmnV3ProcessInstance> GetProcessInstanceByTokenAsync(Guid tokenId);
        Task SaveProcessInstanceAsync(BpmnV3ProcessInstance processInstance);
    }
} 