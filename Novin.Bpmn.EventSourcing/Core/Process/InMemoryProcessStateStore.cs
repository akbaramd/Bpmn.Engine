using System.Collections.Concurrent;

namespace Novin.Bpmn.EventSourcing.Core.Process;

public class InMemoryProcessStateStore : IProcessStateStore
{
    private readonly ConcurrentDictionary<Guid, ProcessState> _states = new();

    public ProcessState? Get(Guid instanceId)
    {
        _states.TryGetValue(instanceId, out var state);
        return state;
    }

    public void Save(ProcessState state)
    {
        _states.AddOrUpdate(state.InstanceId, state, (key, oldState) =>
        {
            if (state.Version <= oldState.Version)
            {
                // نسخه قدیمی‌تر را ذخیره نکن
                return oldState;
            }
            return state;
        });
    }

    public void Remove(Guid instanceId)
    {
        _states.TryRemove(instanceId, out _);
    }

    public void Compact()
    {
        // در نسخه in-memory معمولا نیازی نیست
        // اما اگر نسخه‌های قدیمی داشتیم حذف می‌کردیم اینجا
    }

    public IEnumerable<ProcessState> GetAll()
    {
        return _states.Values.ToList();
    }
}