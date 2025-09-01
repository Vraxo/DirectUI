using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using Silk.NET.Input;
using System.Text.RegularExpressions;

namespace DirectUI;

public class ClickCaptureServer
{
    private readonly List<ClickCaptureRequest> _requests = new();

    public void RequestCapture(int id, int layer)
    {
        _requests.Add(new ClickCaptureRequest(id, layer));
        Console.WriteLine($"[CAPTURE-REQUEST] ID: {id}, Layer: {layer}");
    }

    public int? GetWinner()
    {
        if (_requests.Count == 0)
        {
            return null;
        }

        var winner = _requests.OrderByDescending(r => r.Layer).First();

        // Always log the resolution process if there was at least one candidate.
        var candidates = string.Join(", ", _requests.Select(r => $"[ID: {r.Id}, L: {r.Layer}]"));
        Console.WriteLine($"[CAPTURE-RESOLVED] Candidates: {candidates} -> Winner: ID {winner.Id} (Layer {winner.Layer})");

        return winner.Id;
    }

    public void Clear()
    {
        _requests.Clear();
    }
}