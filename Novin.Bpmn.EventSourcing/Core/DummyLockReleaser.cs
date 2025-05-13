using System;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core
{
    /// <summary>
    /// A dummy implementation of IDisposable that releases nothing but maintains the lock pattern.
    /// Used when a real lock couldn't be acquired to maintain code flow.
    /// </summary>
    public class DummyLockReleaser : IAsyncDisposable, IDisposable
    {
        public static readonly DummyLockReleaser Instance = new DummyLockReleaser();
        
        private DummyLockReleaser() { }
        
        public void Dispose() 
        {
            // Do nothing - this is a dummy releaser
        }
        
        public ValueTask DisposeAsync()
        {
            // Do nothing - this is a dummy releaser
            return ValueTask.CompletedTask;
        }
    }
} 