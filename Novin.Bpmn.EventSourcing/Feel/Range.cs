namespace Novin.Bpmn.EventSourcing.Feel;

public sealed class Range
{
    private readonly dynamic _start;
    private readonly dynamic _end;
    private readonly bool _incStart, _incEnd;

    public Range(dynamic s, dynamic e, bool incStart, bool incEnd)
        => (_start, _end, _incStart, _incEnd) = (s, e, incStart, incEnd);

    public bool Contains(dynamic value)
    {
        bool gteStart = _incStart ? value >= _start : value > _start;
        bool lteEnd   = _incEnd   ? value <= _end   : value < _end;
        return gteStart && lteEnd;
    }
}