using System.Collections.Generic;
using System.Linq;

namespace DirectUI;

public class ClickCaptureServer
{
    private readonly List<ClickCaptureRequest> _requests = new();

    public void RequestCapture(int id, int layer)
    {
        _requests.Add(new ClickCaptureRequest(id, layer));
    }

    public int? GetWinner()
    {
        if (_requests.Count == 0)
        {
            return null;
        }

        return _requests.OrderByDescending(r => r.Layer).First().Id;
    }

    public void Clear()
    {
        _requests.Clear();
    }
}
