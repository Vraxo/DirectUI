using System;

namespace DirectUI;

public interface IAppLogic
{
    void DrawUI(UIContext context);
    // Any other common logic that needs to be called by the host.
    // For this step, just DrawUI.
    // Action OpenProjectWindowAction { get; } // Removed
}
