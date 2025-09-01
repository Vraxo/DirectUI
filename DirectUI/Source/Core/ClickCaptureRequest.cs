namespace DirectUI;

public readonly struct ClickCaptureRequest
{
    public int Id { get; }
    public int Layer { get; }

    public ClickCaptureRequest(int id, int layer)
    {
        Id = id;
        Layer = layer;
    }
}
