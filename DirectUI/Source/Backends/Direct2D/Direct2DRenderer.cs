using System.Numerics;
using DirectUI.Core;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using InputElementDescription = Vortice.Direct3D11.InputElementDescription;

namespace DirectUI.Backends;

/// <summary>
/// A rendering backend that uses Direct2D to implement the IRenderer interface.
/// It now holds a reference to the DuiGraphicsDevice to ensure it always uses a valid render target.
/// </summary>
public class Direct2DRenderer : IRenderer
{
    private readonly DuiGraphicsDevice _graphicsDevice;
    private readonly Dictionary<Color, ID2D1SolidColorBrush> _brushCache = new();

    // D3D resources for the cube
    private ID3D11Buffer? _cubeVertexBuffer;
    private ID3D11Buffer? _cubeIndexBuffer;
    private ID3D11Buffer? _cubeConstantBuffer;
    private ID3D11VertexShader? _cubeVertexShader;
    private ID3D11PixelShader? _cubePixelShader;
    private ID3D11InputLayout? _cubeInputLayout;
    private ID3D11RasterizerState? _cubeRasterizerState;
    private ID3D11DepthStencilState? _cubeDepthStencilState;


    // Internal text layout cache for DrawText method
    private readonly Dictionary<TextLayoutCacheKey, IDWriteTextLayout> _textLayoutCache = new();
    private readonly Dictionary<FontKey, IDWriteTextFormat> _textFormatCache = new();

    // Internal cache key for text layouts (similar to UIResources.TextLayoutCacheKey)
    private readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
    {
        public readonly string Text;
        public readonly FontKey FontKey;
        public readonly Vector2 MaxSize;
        public readonly HAlignment HAlign;
        public readonly VAlignment VAlign;

        public TextLayoutCacheKey(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
        {
            Text = text;
            FontKey = new FontKey(style);
            MaxSize = maxSize;
            HAlign = alignment.Horizontal;
            VAlign = alignment.Vertical;
        }

        public bool Equals(TextLayoutCacheKey other) => Text == other.Text && MaxSize.Equals(other.MaxSize) && HAlign == other.HAlign && VAlign == other.VAlign && FontKey.Equals(other.FontKey);
        public override bool Equals(object? obj) => obj is TextLayoutCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontKey, MaxSize, HAlign, VAlign);
    }

    private readonly struct FontKey : IEquatable<FontKey>
    {
        public readonly string FontName;
        public readonly float FontSize;
        public readonly FontWeight FontWeight;
        public readonly FontStyle FontStyle;
        public readonly FontStretch FontStretch;

        public FontKey(ButtonStyle style)
        {
            FontName = style.FontName;
            FontSize = style.FontSize;
            FontWeight = style.FontWeight;
            FontStyle = style.FontStyle;
            FontStretch = style.FontStretch;
        }

        public bool Equals(FontKey other) => FontName == other.FontName && FontSize.Equals(other.FontSize) && FontWeight == other.FontWeight && FontStyle == other.FontStyle && FontStretch == other.FontStretch;
        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontSize, FontWeight, FontStyle, FontStretch);
    }


    public Vector2 RenderTargetSize
    {
        get
        {
            if (_graphicsDevice.RenderTarget is null)
            {
                return Vector2.Zero;
            }
            var size = _graphicsDevice.RenderTarget.Size;
            return new Vector2(size.Width, size.Height);
        }
    }

    public Direct2DRenderer(DuiGraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        CreateCubeResources();
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        var renderTarget = _graphicsDevice.RenderTarget;
        if (renderTarget is null) return;

        var brush = GetOrCreateBrush(color);
        if (brush is null) return;
        renderTarget.DrawLine(p1, p2, brush, strokeWidth);
    }

    public void DrawBox(Vortice.Mathematics.Rect rect, BoxStyle style)
    {
        var renderTarget = _graphicsDevice.RenderTarget;
        if (renderTarget is null || style is null || rect.Width <= 0 || rect.Height <= 0) return;

        var pos = rect.TopLeft;
        var size = new Vector2(rect.Width, rect.Height);

        ID2D1SolidColorBrush? fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush? borderBrush = GetOrCreateBrush(style.BorderColor);

        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null && (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        if (style.Roundness > 0.0f)
        {
            Vortice.Mathematics.Rect outerBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, size.X, size.Y);
            float maxRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                if (hasVisibleBorder)
                {
                    System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                    renderTarget.FillRoundedRectangle(new RoundedRectangle(outerRectF, radius, radius), borderBrush);
                }
                if (hasVisibleFill)
                {
                    float fillX = pos.X + borderLeft;
                    float fillY = pos.Y + borderTop;
                    float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
                    float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
                    if (fillWidth > 0 && fillHeight > 0)
                    {
                        float avgBorderX = (borderLeft + borderRight) * 0.5f;
                        float avgBorderY = (borderTop + borderBottom) * 0.5f;
                        float innerRadiusX = Math.Max(0f, radius - avgBorderX);
                        float innerRadiusY = Math.Max(0f, radius - avgBorderY);
                        System.Drawing.RectangleF fillRectF = new(fillX, fillY, fillWidth, fillHeight);
                        renderTarget.FillRoundedRectangle(new RoundedRectangle(fillRectF, innerRadiusX, innerRadiusY), fillBrush);
                    }
                    else if (!hasVisibleBorder && fillBrush is not null)
                    {
                        System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                        renderTarget.FillRoundedRectangle(new RoundedRectangle(outerRectF, radius, radius), fillBrush);
                    }
                }
                return;
            }
        }
        if (hasVisibleBorder && borderBrush is not null)
        {
            renderTarget.FillRectangle(rect, borderBrush);
        }
        if (hasVisibleFill)
        {
            float fillX = pos.X + borderLeft;
            float fillY = pos.Y + borderTop;
            float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
            float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
            if (fillWidth > 0 && fillHeight > 0)
            {
                renderTarget.FillRectangle(new Vortice.Mathematics.Rect(fillX, fillY, fillWidth, fillHeight), fillBrush);
            }
            else if (!hasVisibleBorder && fillBrush is not null)
            {
                renderTarget.FillRectangle(rect, fillBrush);
            }
        }
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        var renderTarget = _graphicsDevice.RenderTarget;
        if (renderTarget is null || string.IsNullOrEmpty(text)) return;

        var textBrush = GetOrCreateBrush(color);
        if (textBrush is null) return;

        var layoutKey = new TextLayoutCacheKey(text, style, maxSize, alignment);
        if (!_textLayoutCache.TryGetValue(layoutKey, out var textLayout))
        {
            var textFormat = GetOrCreateTextFormat(style);
            if (textFormat is null || _graphicsDevice.DWriteFactory is null) return;

            textLayout = _graphicsDevice.DWriteFactory.CreateTextLayout(text, textFormat, maxSize.X, maxSize.Y);
            textLayout.TextAlignment = alignment.Horizontal switch
            {
                HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                _ => Vortice.DirectWrite.TextAlignment.Leading
            };
            textLayout.ParagraphAlignment = alignment.Vertical switch
            {
                VAlignment.Top => ParagraphAlignment.Near,
                VAlignment.Center => ParagraphAlignment.Center,
                VAlignment.Bottom => ParagraphAlignment.Far,
                _ => ParagraphAlignment.Near
            };
            _textLayoutCache[layoutKey] = textLayout;
        }

        // A small vertical adjustment to compensate for font metrics making text appear slightly too low when using ParagraphAlignment.Center.
        float yOffsetCorrection = (alignment.Vertical == VAlignment.Center) ? -1.5f : 0f;

        renderTarget.DrawTextLayout(new Vector2(origin.X, origin.Y + yOffsetCorrection), textLayout, textBrush, Vortice.Direct2D1.DrawTextOptions.None);
    }

    public void PushClipRect(Vortice.Mathematics.Rect rect, AntialiasMode antialiasMode)
    {
        _graphicsDevice.RenderTarget?.PushAxisAlignedClip(rect, antialiasMode);
    }

    public void PopClipRect()
    {
        _graphicsDevice.RenderTarget?.PopAxisAlignedClip();
    }

    public void Flush()
    {
        // Direct2D is an immediate-mode API in this context, no flush needed.
    }

    public void Cleanup()
    {
        foreach (var pair in _brushCache)
        {
            pair.Value?.Dispose();
        }
        _brushCache.Clear();

        foreach (var pair in _textLayoutCache)
        {
            pair.Value?.Dispose();
        }
        _textLayoutCache.Clear();

        foreach (var pair in _textFormatCache)
        {
            pair.Value?.Dispose();
        }
        _textFormatCache.Clear();

        _cubeVertexBuffer?.Dispose();
        _cubeIndexBuffer?.Dispose();
        _cubeConstantBuffer?.Dispose();
        _cubeVertexShader?.Dispose();
        _cubePixelShader?.Dispose();
        _cubeInputLayout?.Dispose();
        _cubeRasterizerState?.Dispose();
        _cubeDepthStencilState?.Dispose();
    }

    private ID2D1SolidColorBrush? GetOrCreateBrush(Drawing.Color color)
    {
        var renderTarget = _graphicsDevice.RenderTarget;
        if (renderTarget is null)
        {
            // Don't log here to avoid spam on resize. The caller will just not draw.
            return null;
        }

        if (_brushCache.TryGetValue(color, out var brush) && brush is not null)
        {
            return brush;
        }

        if (brush is null && _brushCache.ContainsKey(color))
        {
            _brushCache.Remove(color);
        }

        try
        {
            Vortice.Mathematics.Color4 color4 = color;
            brush = renderTarget.CreateSolidColorBrush(color4);
            if (brush is not null)
            {
                _brushCache[color] = brush;
                return brush;
            }

            Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
            return null;
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null;
        }
    }

    private IDWriteTextFormat? GetOrCreateTextFormat(ButtonStyle style)
    {
        var dwriteFactory = _graphicsDevice.DWriteFactory;
        if (dwriteFactory is null) return null;

        var key = new FontKey(style);
        if (_textFormatCache.TryGetValue(key, out var format))
        {
            return format;
        }

        try
        {
            var newFormat = dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (newFormat is not null) { _textFormatCache[key] = newFormat; }
            return newFormat;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating text format for font '{style.FontName}': {ex.Message}");
            return null;
        }
    }

    private void CreateCubeResources()
    {
        var d3dDevice = _graphicsDevice.D3DDevice;
        if (d3dDevice is null)
        {
            Console.WriteLine("Cannot create cube resources: D3DDevice is null.");
            return;
        }

        var vertices = new[]
        {
            new Vector4(-1.0f, 1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            new Vector4(1.0f, 1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
            new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
            new Vector4(-1.0f, 1.0f, 1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
            new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
            new Vector4(1.0f, -1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
            new Vector4(1.0f, -1.0f, 1.0f, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            new Vector4(-1.0f, -1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)
        };

        var indices = new ushort[]
        {
            3, 1, 0, 2, 1, 3,
            0, 5, 4, 1, 5, 0,
            3, 4, 7, 0, 4, 3,
            1, 6, 5, 2, 6, 1,
            2, 7, 6, 3, 7, 2,
            6, 4, 5, 7, 4, 6
        };

        _cubeVertexBuffer = d3dDevice.CreateBuffer(vertices, BindFlags.VertexBuffer);
        _cubeIndexBuffer = d3dDevice.CreateBuffer(indices, BindFlags.IndexBuffer);

        using var vertexShaderByteCode = ShaderCompiler.Compile(
@"
cbuffer ConstantBuffer : register(b0)
{
    matrix WorldViewProjection;
}

struct VS_Input
{
    float4 Position : POSITION;
    float4 Color : COLOR;
};

struct PS_Input
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PS_Input VS(VS_Input input)
{
    PS_Input output;
    output.Position = mul(input.Position, WorldViewProjection);
    output.Color = input.Color;
    return output;
}
", "VS", "vs_5_0");

        using var pixelShaderByteCode = ShaderCompiler.Compile(
@"
struct PS_Input
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

float4 PS(PS_Input input) : SV_TARGET
{
    return input.Color;
}
", "PS", "ps_5_0");

        byte[] vsBytes = vertexShaderByteCode.AsBytes();
        byte[] psBytes = pixelShaderByteCode.AsBytes();

        _cubeVertexShader = d3dDevice.CreateVertexShader(vsBytes);
        _cubePixelShader = d3dDevice.CreatePixelShader(psBytes);

        var inputElements = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
        };

        _cubeInputLayout = d3dDevice.CreateInputLayout(inputElements, vsBytes);
        _cubeConstantBuffer = d3dDevice.CreateBuffer(new BufferDescription(64, BindFlags.ConstantBuffer));

        var rasterizerDesc = new RasterizerDescription(CullMode.Back, Vortice.Direct3D11.FillMode.Solid);
        _cubeRasterizerState = d3dDevice.CreateRasterizerState(rasterizerDesc);

        var depthStencilDesc = new DepthStencilDescription
        {
            DepthEnable = true,
            DepthWriteMask = DepthWriteMask.All,
            DepthFunc = ComparisonFunction.Less,
            StencilEnable = false
        };
        _cubeDepthStencilState = d3dDevice.CreateDepthStencilState(depthStencilDesc);
    }


    public void DrawCube()
    {
        var d3dDevice = _graphicsDevice.D3DDevice;
        var d3dContext = _graphicsDevice.D3DContext;
        var swapChain = _graphicsDevice.SwapChain;
        var depthStencilView = _graphicsDevice.DepthStencilView;

        if (d3dDevice is null || d3dContext is null || swapChain is null || depthStencilView is null) return;
        if (_cubeConstantBuffer is null || _cubeInputLayout is null || _cubeVertexShader is null || _cubePixelShader is null || _cubeVertexBuffer is null || _cubeIndexBuffer is null || _cubeRasterizerState is null || _cubeDepthStencilState is null)
        {
            return;
        }

        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, -5), Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 4.0f, RenderTargetSize.X / RenderTargetSize.Y, 0.1f, 100.0f);
        var world = Matrix4x4.CreateFromYawPitchRoll(
            (float)DateTime.Now.TimeOfDay.TotalSeconds,
            (float)DateTime.Now.TimeOfDay.TotalSeconds / 2f,
            (float)DateTime.Now.TimeOfDay.TotalSeconds / 3f);

        var worldViewProj = Matrix4x4.Transpose(world * view * proj);
        d3dContext.UpdateSubresource(worldViewProj, _cubeConstantBuffer);

        d3dContext.RSSetViewport(new Viewport(RenderTargetSize.X, RenderTargetSize.Y));
        d3dContext.RSSetState(_cubeRasterizerState);
        d3dContext.OMSetDepthStencilState(_cubeDepthStencilState);

        d3dContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        d3dContext.IASetInputLayout(_cubeInputLayout);
        d3dContext.VSSetShader(_cubeVertexShader);
        d3dContext.VSSetConstantBuffer(0, _cubeConstantBuffer);
        d3dContext.PSSetShader(_cubePixelShader);
        d3dContext.IASetVertexBuffer(0, _cubeVertexBuffer, 32);
        d3dContext.IASetIndexBuffer(_cubeIndexBuffer, Format.R16_UInt, 0);

        using (var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0))
        {
            using var renderTargetView = d3dDevice.CreateRenderTargetView(backBuffer);
            d3dContext.ClearRenderTargetView(renderTargetView, Colors.CornflowerBlue);
            d3dContext.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
            d3dContext.OMSetRenderTargets([renderTargetView], depthStencilView);
            d3dContext.DrawIndexed(36, 0, 0);
        }

        // Unbind render targets before flushing, to release control for Direct2D
        d3dContext.OMSetRenderTargets(new ID3D11RenderTargetView(0));

        // Flush the 3D commands to ensure they are executed before D2D begins drawing.
        d3dContext.Flush();
    }
}