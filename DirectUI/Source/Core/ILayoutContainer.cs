using System.Numerics;

namespace DirectUI;

public interface ILayoutContainer
{
    Vector2 GetCurrentPosition();
    void Advance(Vector2 elementSize);
}