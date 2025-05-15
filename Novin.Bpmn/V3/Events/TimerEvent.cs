using Novin.Bpmn.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.V3.Events
{
    /// <summary>
    /// Represents a timer event in BPMN
    /// </summary>
    public class TimerEvent : BpmnEventBase<BpmnTimerEventDefinition>
    {
        private Timer _timer;
        private readonly object _timerLock = new object();
        private bool _isDisposed = false;
        
        // The duration in milliseconds
        public int DurationMs { get; private set; }
        
        // The timer type and expression
        public string TimerType { get; private set; }
        public string TimerValue { get; private set; }
        
        public TimerEvent(BpmnTimerEventDefinition eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, 
            bool isInterrupting, BpmnV3ProcessInstance processInstance) 
            : base(eventDefinition, boundaryEvent, token, isInterrupting, processInstance)
        {
            // Get timer type and value
            var (timerType, timerValue) = eventDefinition.GetTimerTypeAndValue();
            TimerType = timerType;
            TimerValue = timerValue;
            
            // Parse the timer expression
            DurationMs = ParseTimerExpression(TimerType, TimerValue);
        }
        
        public override Task Initialize()
        {
            Console.WriteLine($"Initializing Timer Event with type {TimerType}, value {TimerValue}, duration {DurationMs}ms for {BoundaryEvent?.id ?? "unknown"} attached to {BoundaryEvent?.attachedToRef?.Name ?? "unknown"}");
            
            // Create and start the timer
            StartTimer();
            
            return base.Initialize();
        }
        
        private void StartTimer()
        {
            lock (_timerLock)
            {
                if (_isDisposed) return;
                
                // Create a timer that triggers after the specified duration
                _timer = new Timer(
                    async state => await TimerCallback(state),
                    null,
                    DurationMs,
                    Timeout.Infinite); // Only trigger once
            }
        }
        
        private async Task TimerCallback(object state)
        {
            try
            {
                await Trigger();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error triggering timer event: {ex.Message}");
            }
        }
        
        public override Task Cancel()
        {
            DisposeTimer();
            return base.Cancel();
        }
        
        private void DisposeTimer()
        {
            lock (_timerLock)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
                
                _isDisposed = true;
            }
        }
        
        private int ParseTimerExpression(string timerType, string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return 5000; // Default to 5 seconds if no expression
            }
            
            try
            {
                // Handle different timer types
                switch (timerType)
                {
                    case "timeDuration":
                        return ParseDuration(expression);
                        
                    case "timeDate":
                        return ParseDateToMilliseconds(expression);
                        
                    case "timeCycle":
                        // For simplicity, we'll treat cycle as a one-time duration for this implementation
                        return ParseDuration(expression);
                        
                    default:
                        return 5000; // Default to 5 seconds
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing timer expression '{expression}': {ex.Message}");
                return 5000; // Default to 5 seconds on error
            }
        }
        
        private int ParseDuration(string expression)
        {
            // Handle ISO 8601 duration format (e.g., PT1H30M45S)
            if (expression.StartsWith("PT"))
            {
                // Remove PT prefix
                string durationPart = expression.Substring(2);
                
                int totalMs = 0;
                
                // Parse hours
                int hIndex = durationPart.IndexOf('H');
                if (hIndex > 0)
                {
                    int hours = int.Parse(durationPart.Substring(0, hIndex));
                    totalMs += hours * 3600000; // Convert hours to milliseconds
                    durationPart = durationPart.Substring(hIndex + 1);
                }
                
                // Parse minutes
                int mIndex = durationPart.IndexOf('M');
                if (mIndex > 0)
                {
                    int minutes = int.Parse(durationPart.Substring(0, mIndex));
                    totalMs += minutes * 60000; // Convert minutes to milliseconds
                    durationPart = durationPart.Substring(mIndex + 1);
                }
                
                // Parse seconds
                int sIndex = durationPart.IndexOf('S');
                if (sIndex > 0)
                {
                    // Handle decimal seconds
                    string secondsPart = durationPart.Substring(0, sIndex);
                    if (secondsPart.Contains("."))
                    {
                        double seconds = double.Parse(secondsPart);
                        totalMs += (int)(seconds * 1000); // Convert seconds to milliseconds
                    }
                    else
                    {
                        int seconds = int.Parse(secondsPart);
                        totalMs += seconds * 1000; // Convert seconds to milliseconds
                    }
                }
                
                return totalMs > 0 ? totalMs : 1000; // Minimum 1 second
            }
            
            // For simple integer values (assume milliseconds)
            if (int.TryParse(expression, out int ms))
            {
                return ms;
            }
            
            // Default
            return 5000;
        }
        
        private int ParseDateToMilliseconds(string dateExpression)
        {
            try
            {
                // Try to parse as DateTime
                if (DateTime.TryParse(dateExpression, out DateTime targetDate))
                {
                    // Calculate milliseconds from now to the target date
                    TimeSpan timeUntilTarget = targetDate - DateTime.Now;
                    
                    // If the date is in the future, return the milliseconds until then
                    if (timeUntilTarget.TotalMilliseconds > 0)
                    {
                        return (int)timeUntilTarget.TotalMilliseconds;
                    }
                    
                    // If the date is in the past, trigger immediately
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing date expression '{dateExpression}': {ex.Message}");
            }
            
            // Default value if parsing fails
            return 5000;
        }
    }
    
    /// <summary>
    /// Handler for timer events
    /// </summary>
    public class TimerEventHandler : BpmnEventHandlerBase<BpmnTimerEventDefinition, TimerEvent>
    {
        private readonly ConcurrentDictionary<Guid, List<TimerEvent>> _tokenEvents = new ConcurrentDictionary<Guid, List<TimerEvent>>();
        
        public TimerEventHandler(BpmnV3ProcessInstance processInstance) : base(processInstance)
        {
        }
        
        public override async Task<TimerEvent> CreateEvent(BpmnTimerEventDefinition eventDefinition, BpmnBoundaryEvent boundaryEvent, BpmnV3Token token, bool isInterrupting)
        {
            var timerEvent = new TimerEvent(eventDefinition, boundaryEvent, token, isInterrupting, ProcessInstance);
            
            // Register the event with the token
            if (!_tokenEvents.TryGetValue(token.Id, out var events))
            {
                events = new List<TimerEvent>();
                _tokenEvents[token.Id] = events;
            }
            
            events.Add(timerEvent);
            
            return timerEvent;
        }
        
        public override async Task<bool> TriggerEvents(Guid tokenId)
        {
            if (!_tokenEvents.TryGetValue(tokenId, out var events) || !events.Any())
                return false;
            
            Console.WriteLine($"Triggering {events.Count} timer events for token {tokenId}");
            
            // Trigger all timer events for this token
            foreach (var timerEvent in events)
            {
                await timerEvent.Trigger();
            }
            
            return events.Any(e => e.IsTriggered);
        }
        
        public override async Task CancelEvents(Guid tokenId)
        {
            if (!_tokenEvents.TryGetValue(tokenId, out var events))
                return;
            
            foreach (var timerEvent in events)
            {
                await timerEvent.Cancel();
            }
            
            _tokenEvents.TryRemove(tokenId, out _);
        }
    }
} 