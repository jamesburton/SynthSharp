using SynthSharp.Core.Layout;

namespace SynthSharp.Input;

public sealed class PadTriggerRouter
{
    private readonly Dictionary<string, string> _padByKey = new(StringComparer.OrdinalIgnoreCase);

    public PadTriggerRouter(IEnumerable<PadAssignment> pads)
    {
        Rebuild(pads);
    }

    public bool TryResolvePad(string key, out string padId)
    {
        return _padByKey.TryGetValue(key, out padId!);
    }

    public void Rebuild(IEnumerable<PadAssignment> pads)
    {
        _padByKey.Clear();
        foreach (var pad in pads)
        {
            if (!string.IsNullOrWhiteSpace(pad.KeyBinding))
            {
                _padByKey[pad.KeyBinding.Trim()] = pad.PadId;
            }
        }
    }
}
