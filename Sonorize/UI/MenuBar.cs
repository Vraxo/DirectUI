// Sonorize/Source/UI/MenuBar.cs
using System;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class MenuBar
{
    private readonly Action _onOpenSettings;

    public MenuBar(Action onOpenSettings)
    {
        _onOpenSettings = onOpenSettings ?? throw new ArgumentNullException(nameof(onOpenSettings));
    }

    public void Draw(UIContext context)
    {
        // This version's UI design does not include a visible top menu bar.
        // The logic is kept minimal and can be expanded if needed.
    }
}