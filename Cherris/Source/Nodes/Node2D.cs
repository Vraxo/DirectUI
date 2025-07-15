namespace Cherris;

public class Node2D : VisualItem
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public virtual float Rotation { get; set; } = 0;
    public OriginPreset OriginPreset { get; set; } = OriginPreset.Center;    public bool InheritScale { get; set; } = true;
    public HAlignment HAlignment { get; set; } = HAlignment.Center;
    public VAlignment VAlignment { get; set; } = VAlignment.Center;
    public AnchorPreset AnchorPreset { get; set; } = AnchorPreset.None;

    public float MarginLeft { get; set; } = 0f;
    public float MarginTop { get; set; } = 0f;
    public float MarginRight { get; set; } = 0f;
    public float MarginBottom { get; set; } = 0f;

    public float RelativeWidth { get; set; } = 0f;    public float RelativeHeight { get; set; } = 0f;
    public Vector2 ScaledSize => Size * Scale;

    public virtual Vector2 Size
    {
        get
        {
            float finalWidth;
            float finalHeight;
            Vector2 parentSizeForRelative = Vector2.Zero;
            if (RelativeWidth > 0f && RelativeWidth <= 1f)
            {
                if (parentSizeForRelative == Vector2.Zero)
                    parentSizeForRelative = (Parent is Node2D p) ? p.Size : GetWindowSizeV2();
                finalWidth = parentSizeForRelative.X * RelativeWidth;
            }
            else if (_explicitSize.X != 0f)
            {
                finalWidth = _explicitSize.X;
            }
            else
            {
                finalWidth = ComputeAutoSize().X;
            }
            if (RelativeHeight > 0f && RelativeHeight <= 1f)
            {
                if (parentSizeForRelative == Vector2.Zero)
                    parentSizeForRelative = (Parent is Node2D p) ? p.Size : GetWindowSizeV2();
                finalHeight = parentSizeForRelative.Y * RelativeHeight;
            }
            else if (_explicitSize.Y != 0f)
            {
                finalHeight = _explicitSize.Y;
            }
            else
            {
                finalHeight = ComputeAutoSize().Y;
            }

            return new Vector2(finalWidth, finalHeight);
        }
        set
        {
            if (_explicitSize == value) return;
            _explicitSize = value;
            SizeChanged?.Invoke(this, Size);
        }
    }

    public virtual Vector2 Scale
    {
        get => InheritScale && Parent is Node2D node2DParent ? node2DParent.Scale : fieldScale;        set;    } = new(1, 1);

    [HideFromInspector]
    public virtual Vector2 GlobalPosition
    {
        get
        {
            Vector2 parentGlobalTopLeft;
            Vector2 parentSize;

            if (Parent is Node2D parentNode)
            {
                parentGlobalTopLeft = parentNode.GlobalPosition - parentNode.Origin;
                parentSize = parentNode.Size;
            }
            else
            {
                parentGlobalTopLeft = Vector2.Zero;
                parentSize = GetWindowSizeV2();
            }

            float calculatedGlobalOriginX;
            float calculatedGlobalOriginY;

            if (AnchorPreset == AnchorPreset.None)
            {
                Vector2 parentOriginGlobal = (Parent is Node2D pNode) ? pNode.GlobalPosition : Vector2.Zero;
                calculatedGlobalOriginX = parentOriginGlobal.X + Position.X;
                calculatedGlobalOriginY = parentOriginGlobal.Y + Position.Y;
            }
            else
            {
                float targetGlobalAnchorX = AnchorPreset switch
                {
                    AnchorPreset.TopLeft or AnchorPreset.CenterLeft or AnchorPreset.BottomLeft
                        => parentGlobalTopLeft.X + MarginLeft,
                    AnchorPreset.TopCenter or AnchorPreset.Center or AnchorPreset.BottomCenter
                        => parentGlobalTopLeft.X + (parentSize.X * 0.5f) + MarginLeft - MarginRight,
                    _
                        => parentGlobalTopLeft.X + parentSize.X - MarginRight,
                };

                float targetGlobalAnchorY = AnchorPreset switch
                {
                    AnchorPreset.TopLeft or AnchorPreset.TopCenter or AnchorPreset.TopRight
                        => parentGlobalTopLeft.Y + MarginTop,
                    AnchorPreset.CenterLeft or AnchorPreset.Center or AnchorPreset.CenterRight
                        => parentGlobalTopLeft.Y + (parentSize.Y * 0.5f) + MarginTop - MarginBottom,
                    _
                        => parentGlobalTopLeft.Y + parentSize.Y - MarginBottom,
                };

                calculatedGlobalOriginX = targetGlobalAnchorX;
                calculatedGlobalOriginY = targetGlobalAnchorY;

                calculatedGlobalOriginX += Position.X;
                calculatedGlobalOriginY += Position.Y;
            }

            return new(calculatedGlobalOriginX, calculatedGlobalOriginY);
        }
    }

    public Vector2 Offset { get; set; }

    public Vector2 Origin
    {
        get
        {
            float x = HAlignment switch
            {
                HAlignment.Center => Size.X / 2f,
                HAlignment.Left => 0,
                HAlignment.Right => Size.X,
                HAlignment.None => 0,
                _ => 0
            };

            float y = VAlignment switch
            {
                VAlignment.Center => Size.Y / 2f,
                VAlignment.Top => 0,
                VAlignment.Bottom => Size.Y,
                VAlignment.None => 0,
                _ => 0
            };

            Vector2 alignmentOffset = new(x, y);
            return alignmentOffset + Offset;
        }
    }

    protected Vector2 _explicitSize = Vector2.Zero;
    private Vector2 fieldScale = new(1, 1);

    public event EventHandler<Vector2>? SizeChanged;

    protected virtual Vector2 ComputeAutoSize()
    {
        return Vector2.Zero;    }

    public void LookAt(Vector2 targetPosition)
    {
        Vector2 originPoint = GlobalPosition;
        Vector2 direction = targetPosition - originPoint;
        var angle = float.Atan2(direction.Y, direction.X) * 57.29578f;
        Rotation = angle;
    }
}