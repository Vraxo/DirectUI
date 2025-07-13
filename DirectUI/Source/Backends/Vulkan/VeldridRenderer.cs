using DirectUI.Backends.Vulkan;
using DirectUI.Core;
using SharpText.Veldrid;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Veldrid;
using Vortice.Direct2D1;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DirectUI.Core;
using SharpText.Core;
using SharpText.Veldrid;
using Veldrid;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace DirectUI.Backends.Vulkan;

public class VeldridRenderer : IRenderer, IDisposable
{
    private readonly Veldrid.GraphicsDevice _gd;
    private readonly CommandList _cl;
    private readonly Pipeline _pipeline;
    private readonly ResourceLayout _projMatrixLayout;
    private readonly ResourceSet _projMatrixSet;
    private readonly DeviceBuffer _projMatrixBuffer;
    private readonly DeviceBuffer _vertexBuffer;
    private readonly DeviceBuffer _indexBuffer;
    private readonly uint _vertexBufferSize = 2048;
    private readonly uint _indexBufferSize = 4096;

    // Font is persistent, but the renderer will be created per-batch.
    private readonly SharpText.Core.Font _font;

    // Batched text rendering state
    private readonly struct BatchedText
    {
        public readonly Vector2 Origin;
        public readonly string Text;
        public readonly ButtonStyle Style;
        public readonly Alignment Alignment;
        public readonly Vector2 MaxSize;
        public readonly Drawing.Color Color;

        public BatchedText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
        {
            Origin = origin;
            Text = text;
            Style = style;
            Alignment = alignment;
            MaxSize = maxSize;
            Color = color;
        }
    }
    private readonly List<BatchedText> _textBatch = new();


    public Vector2 RenderTargetSize
    {
        get
        {
            return new(
                _gd.MainSwapchain.Framebuffer.Width,
                _gd.MainSwapchain.Framebuffer.Height);
        }
    }

    public VeldridRenderer(Veldrid.GraphicsDevice gd, CommandList cl)
    {
        _gd = gd;
        _cl = cl;

        // Create persistent font resource. The renderer will be transient.
        // Switched to a simpler font to diagnose the SharpText crash.
        _font = new SharpText.Core.Font("C:/Windows/Fonts/consola.ttf", 16);

        // Create resources for flat geometry
        _vertexBuffer = _gd.ResourceFactory.CreateBuffer(new(_vertexBufferSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _indexBuffer = _gd.ResourceFactory.CreateBuffer(new(_indexBufferSize, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        _projMatrixBuffer = _gd.ResourceFactory.CreateBuffer(new((uint)Unsafe.SizeOf<Matrix4x4>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));

        _projMatrixLayout = _gd.ResourceFactory.CreateResourceLayout(
            new(
                new ResourceLayoutElementDescription("ProjectionMatrix", ResourceKind.UniformBuffer, ShaderStages.Vertex)
            )
        );

        _projMatrixSet = _gd.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(_projMatrixLayout, _projMatrixBuffer)
        );

        ShaderDescription vertexShaderDesc = new(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VeldridShaders.FlatVertexShader), "main");
        ShaderDescription fragmentShaderDesc = new(ShaderStages.Fragment, Encoding.UTF8.GetBytes(VeldridShaders.FlatFragmentShader), "main");

        var vertexShader = _gd.ResourceFactory.CreateShader(vertexShaderDesc);
        var fragmentShader = _gd.ResourceFactory.CreateShader(fragmentShaderDesc);
        Shader[] shaders = { vertexShader, fragmentShader };

        GraphicsPipelineDescription pipelineDesc = new()
        {
            BlendState = BlendStateDescription.SingleAlphaBlend,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            RasterizerState = RasterizerStateDescription.Default,
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = [_projMatrixLayout],
            ShaderSet = new ShaderSetDescription(
                [
                    new VertexLayoutDescription(
                        (uint)Unsafe.SizeOf<VertexPositionColor>(),
                        new VertexElementDescription("a_position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("a_color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
                    )
                ],
                shaders
            ),
            Outputs = _gd.MainSwapchain.Framebuffer.OutputDescription
        };

        _pipeline = _gd.ResourceFactory.CreateGraphicsPipeline(pipelineDesc);

        vertexShader.Dispose();
        fragmentShader.Dispose();
    }

    public void BeginFrame()
    {
        var projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0,
            _gd.MainSwapchain.Framebuffer.Width,
            _gd.MainSwapchain.Framebuffer.Height,
            0,
            -1.0f,
            1.0f);

        _cl.UpdateBuffer(_projMatrixBuffer, 0, ref projectionMatrix);
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        // TODO: Implement line drawing, e.g., by creating a thin quad.
    }

    public void DrawBox(Rect rect, BoxStyle style)
    {
        Vortice.Mathematics.Color4 fillColor = style.FillColor;

        VertexPositionColor[] vertices =
        [
            new(new(rect.Left, rect.Top), fillColor),
            new(new(rect.Right, rect.Top), fillColor),
            new(new(rect.Right, rect.Bottom), fillColor),
            new(new(rect.Left, rect.Bottom), fillColor),
        ];

        ushort[] indices = [0, 1, 2, 0, 2, 3];

        _cl.UpdateBuffer(_vertexBuffer, 0, vertices);
        _cl.UpdateBuffer(_indexBuffer, 0, indices);

        _cl.SetVertexBuffer(0, _vertexBuffer);
        _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _cl.SetPipeline(_pipeline);
        _cl.SetGraphicsResourceSet(0, _projMatrixSet);

        _cl.DrawIndexed(
            indexCount: (uint)indices.Length,
            instanceCount: 1,
            indexStart: 0,
            vertexOffset: 0,
            instanceStart: 0);
    }

    private Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        const float characterWidthApproximationFactor = 0.6f; // Heuristic value
        float width = text.Length * style.FontSize * characterWidthApproximationFactor;
        float height = style.FontSize;
        return new Vector2(width, height);
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Add the text call to the batch to be processed later in Flush().
        _textBatch.Add(new BatchedText(origin, text, style, alignment, maxSize, color));
    }

    public void Flush()
    {
        if (_textBatch.Count == 0)
        {
            return;
        }

        // The SharpText renderer has a default internal buffer limit of 1024 glyphs.
        // We batch our text calls to respect this limit.
        const int maxCharsPerBatch = 1000;

        var currentMiniBatch = new List<BatchedText>();
        int currentMiniBatchCharCount = 0;

        foreach (var textCall in _textBatch)
        {
            string textForLen = textCall.Text;
            if (textForLen.Length > maxCharsPerBatch)
            {
                textForLen = textForLen.Substring(0, maxCharsPerBatch);
            }

            // If the current text call would overflow the mini-batch, process the current one first.
            if (currentMiniBatch.Count > 0 && currentMiniBatchCharCount + textForLen.Length > maxCharsPerBatch)
            {
                ProcessMiniBatch(currentMiniBatch);
                currentMiniBatch.Clear();
                currentMiniBatchCharCount = 0;
            }

            currentMiniBatch.Add(textCall);
            currentMiniBatchCharCount += textForLen.Length;
        }

        // Process the final mini-batch if it has any content.
        if (currentMiniBatch.Count > 0)
        {
            ProcessMiniBatch(currentMiniBatch);
        }

        _textBatch.Clear();
    }

    private void ProcessMiniBatch(List<BatchedText> batch)
    {
        // Create a new, clean renderer for each batch. This is the key to preventing state corruption.
        using var textRenderer = new VeldridTextRenderer(_gd, _cl, _font);

        foreach (var textCall in batch)
        {
            string textToDraw = textCall.Text;
            // The text must still be truncated here as its original form is stored in the BatchedText struct.
            if (textToDraw.Length > 1000)
            {
                textToDraw = textToDraw.Substring(0, 1000);
            }

            Vector2 measuredSize = MeasureText(textToDraw, textCall.Style);
            Vector2 textDrawPos = textCall.Origin;

            // Alignment logic
            if (textCall.MaxSize.X > 0)
            {
                switch (textCall.Alignment.Horizontal)
                {
                    case HAlignment.Center: textDrawPos.X += (textCall.MaxSize.X - measuredSize.X) / 2f; break;
                    case HAlignment.Right: textDrawPos.X += (textCall.MaxSize.X - measuredSize.X); break;
                }
            }
            if (textCall.MaxSize.Y > 0)
            {
                switch (textCall.Alignment.Vertical)
                {
                    case VAlignment.Center: textDrawPos.Y += (textCall.MaxSize.Y - measuredSize.Y) / 2f; break;
                    case VAlignment.Bottom: textDrawPos.Y += (textCall.MaxSize.Y - measuredSize.Y); break;
                }
            }

            var sharpTextColor = new SharpText.Core.Color(textCall.Color.R, textCall.Color.G, textCall.Color.B, textCall.Color.A);
            textRenderer.DrawText(textToDraw, textDrawPos, sharpTextColor, 1);
        }

        // Issue the single draw call for this entire mini-batch.
        textRenderer.Draw();
    }


    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
    {
        _cl.SetScissorRect(
            0,
            (uint)rect.X,
            (uint)rect.Y,
            (uint)rect.Width,
            (uint)rect.Height);
    }

    public void PopClipRect()
    {
        _cl.SetScissorRect(
            0,
            0,
            0,
            _gd.MainSwapchain.Framebuffer.Width,
            _gd.MainSwapchain.Framebuffer.Height);
    }

    public void Cleanup()
    {
        _textBatch.Clear();
        _pipeline.Dispose();
        _projMatrixLayout.Dispose();
        _projMatrixSet.Dispose();
        _projMatrixBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    private bool _disposedValue;
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Cleanup();
            }
            _disposedValue = true;
        }
    }
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}