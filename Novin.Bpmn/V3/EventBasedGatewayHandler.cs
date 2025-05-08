using Novin.Bpmn.Models;
using System;
using System.Linq;

namespace Novin.Bpmn.V3
{
    /// <summary>
    /// مدیریت‌کننده گیت‌وی مبتنی بر رویداد (Event-Based Gateway)
    /// </summary>
    public class EventBasedGatewayHandler
    {
        private readonly BpmnV3ProcessInstance _processInstance;
        
        public EventBasedGatewayHandler(BpmnV3ProcessInstance processInstance)
        {
            _processInstance = processInstance;
        }
        
        /// <summary>
        /// مدیریت گیت‌وی مبتنی بر رویداد
        /// </summary>
        public void HandleEventBasedGateway(BpmnV3Token token, BpmnEventBasedGateway gateway, bool? isExecutable)
        {
            // بررسی وضعیت اجرایی توکن
            bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
            
            // در Event-Based Gateway، توکن منتظر می‌ماند تا یکی از رویدادهای بعدی رخ دهد
            // همه مسیرهای خروجی باید به یک رویداد متصل باشند
            
            var outgoingFlows = _processInstance.DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
            
            if (!tokenIsExecutable)
            {
                // اگر توکن غیرفعال است، همه مسیرها را به صورت غیرفعال ثبت می‌کنیم
                foreach (var flow in outgoingFlows)
                {
                    _processInstance.TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
                    var targetElement = _processInstance.DefinitionsHandler.GetElementById(flow.targetRef);
                    if (targetElement != null)
                    {
                        _processInstance.TrackNodeExecution(targetElement.id, token.Id, false);
                        
                        // بررسی مسیرهای خروجی از رویداد
                        var eventOutgoingFlows = _processInstance.DefinitionsHandler.GetOutgoingSequenceFlows(targetElement);
                        foreach (var eventFlow in eventOutgoingFlows)
                        {
                            _processInstance.TrackFlowExecution(eventFlow.id, token.Id, Guid.Empty, false);
                            _processInstance.TrackNodeExecution(eventFlow.targetRef, token.Id, false);
                        }
                    }
                }
                
                // توکن را تکمیل می‌کنیم
                token.Complete();
                return;
            }
            
            // در این مرحله، توکن به حالت انتظار می‌رود و برای هر رویداد یک مسیر مشخص می‌شود
            // در اینجا ما فقط رویدادها را به عنوان مسیرهای ممکن ثبت می‌کنیم و منتظر می‌مانیم
            
            Console.WriteLine($"Event-Based Gateway {gateway.id} is waiting for one of the following events to occur:");
            
            // رویدادهای ممکن را بررسی می‌کنیم
            foreach (var flow in outgoingFlows)
            {
                var targetElement = _processInstance.DefinitionsHandler.GetElementById(flow.targetRef);
                if (targetElement != null)
                {
                    // ثبت استفاده از فلو به صورت غیرفعال (فعلاً)
                    _processInstance.TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
                    _processInstance.TrackNodeExecution(targetElement.id, token.Id, false);
                    
                    Console.WriteLine($"- Event: {targetElement.id} ({targetElement.GetType().Name})");
                }
            }
            
            // توکن را به حالت انتظار می‌بریم
            // در یک پیاده‌سازی کامل، باید مکانیزمی برای فعال کردن یکی از رویدادها و ادامه جریان پیاده‌سازی شود
            token.SetWaiting();
            
            Console.WriteLine($"Token {token.Id} is waiting at Event-Based Gateway {gateway.id}");
        }
    }
} 