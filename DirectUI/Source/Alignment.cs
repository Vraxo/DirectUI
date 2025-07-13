// Alignment.cs
// Alignment.cs
using DirectUI.Core; // Added using directive

namespace DirectUI;

public enum HAlignment { Left, Center, Right }
public enum VAlignment { Top, Center, Bottom }

public struct Alignment(HAlignment horizontal, VAlignment vertical)
{
    public HAlignment Horizontal { get; set; } = horizontal;
    public VAlignment Vertical { get; set; } = vertical;
}
