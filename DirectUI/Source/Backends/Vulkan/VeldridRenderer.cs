using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DirectUI.Core;
using Veldrid;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace DirectUI.Backends.Vulkan;

public class VeldridRenderer : IRenderer, IDisposable
{
    private readonly Veldrid.GraphicsDevice _gd;
    private readonly CommandList _cl;
    private readonly VulkanFontManager _fontManager;

    // Resources for flat (untextured) drawing
    private readonly Pipeline _flatPipeline;
    private readonly ResourceLayout _projMatrixLayout;
    private readonly ResourceSet _projMatrixSet;
    private readonly DeviceBuffer _projMatrixBuffer;
    private readonly DeviceBuffer _flatVertexBuffer;
    private readonly DeviceBuffer _flatIndexBuffer;
    private readonly uint _flatVertexBufferSize = 2048;
    private readonly uint _flatIndexBufferSize = 4096;

    // Resources for textured (text) drawing
    private readonly Pipeline _textPipeline;
    private readonly ResourceLayout _textTextureLayout;
    private readonly DeviceBuffer _textVertexBuffer;
    private readonly DeviceBuffer _textIndexBuffer;
    private readonly uint _textVertexBufferSize = 8192;
    private readonly uint _textIndexBufferSize = 12288;

    public Vector2 RenderTargetSize => new(_gd.MainSwapchain.Framebuffer.Width, _gd.MainSwapchain.Framebuffer.Height);

    public VeldridRenderer(Veldrid.GraphicsDevice gd, CommandList cl, VulkanFontManager fontManager)
    {
        _gd = gd;
        _cl = cl;
        _fontManager = fontManager;

        // --- Common Resources ---
        _projMatrixBuffer = _gd.ResourceFactory.CreateBuffer(new((uint)Unsafe.SizeOf<Matrix4x4>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        _projMatrixLayout = _gd.ResourceFactory.CreateResourceLayout(
            new(new ResourceLayoutElementDescription("ProjectionMatrix", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
        _projMatrixSet = _gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_projMatrixLayout, _projMatrixBuffer));

        // --- Flat Pipeline ---
        _flatVertexBuffer = _gd.ResourceFactory.CreateBuffer(new(_flatVertexBufferSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _flatIndexBuffer = _gd.ResourceFactory.CreateBuffer(new(_flatIndexBufferSize, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        {
            ShaderDescription vertexShaderDesc = new(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VeldridShaders.FlatVertexShader), "main");
            ShaderDescription fragmentShaderDesc = new(ShaderStages.Fragment, Encoding.UTF8.GetBytes(VeldridShaders.FlatFragmentShader), "main");
            Shader[] shaders = { _gd.ResourceFactory.CreateShader(vertexShaderDesc), _gd.ResourceFactory.CreateShader(fragmentShaderDesc) };

            _flatPipeline = _gd.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription
            {
                BlendState = BlendStateDescription.SingleAlphaBlend,
                DepthStencilState = DepthStencilStateDescription.Disabled,
                RasterizerState = RasterizerStateDescription.Default,
                PrimitiveTopology = PrimitiveTopology.TriangleList,
                ResourceLayouts = [_projMatrixLayout],
                ShaderSet = new ShaderSetDescription(
                    [new VertexLayoutDescription(
                        (uint)Unsafe.SizeOf<VertexPositionColor>(),
                        new VertexElementDescription("a_position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("a_color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4))],
                    shaders),
                Outputs = _gd.MainSwapchain.Framebuffer.OutputDescription
            });
            foreach (var shader in shaders) shader.Dispose();
        }

        // --- Text Pipeline ---
        _textVertexBuffer = _gd.ResourceFactory.CreateBuffer(new(_textVertexBufferSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _textIndexBuffer = _gd.ResourceFactory.CreateBuffer(new(_textIndexBufferSize, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        _textTextureLayout = _gd.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("u_texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("u_sampler", ResourceKind.Sampler, ShaderStages.Fragment)));
        {
            ShaderDescription vertexShaderDesc = new(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VeldridShaders.TexturedVertexShader), "main");
            ShaderDescription fragmentShaderDesc = new(ShaderStages.Fragment, Encoding.UTF8.GetBytes(VeldridShaders.TexturedFragmentShader), "main");
            Shader[] shaders = { _gd.ResourceFactory.CreateShader(vertexShaderDesc), _gd.ResourceFactory.CreateShader(fragmentShaderDesc) };

            _textPipeline = _gd.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription
            {
                BlendState = BlendStateDescription.SingleAlphaBlend,
                DepthStencilState = DepthStencilStateDescription.Disabled,
                RasterizerState = RasterizerStateDescription.Default,
                PrimitiveTopology = PrimitiveTopology.TriangleList,
                ResourceLayouts = [_projMatrixLayout, _textTextureLayout],
                ShaderSet = new ShaderSetDescription(
                    [new VertexLayoutDescription(
                        (uint)Unsafe.SizeOf<VertexPositionTextureColor>(),
                        new VertexElementDescription("a_position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("a_texCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("a_color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4))],
                    shaders),
                Outputs = _gd.MainSwapchain.Framebuffer.OutputDescription
            });
            foreach (var shader in shaders) shader.Dispose();
        }
    }

    public void BeginFrame()
    {
        var projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, RenderTargetSize.X, RenderTargetSize.Y, 0, -1.0f, 1.0f);
        _cl.UpdateBuffer(_projMatrixBuffer, 0, ref projectionMatrix);
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        // TODO: Implement line drawing
    }

    public void DrawBox(Rect rect, BoxStyle style)
    {
        Color4 fillColor = style.FillColor;
        VertexPositionColor[] vertices =
        [
            new(new(rect.Left, rect.Top), fillColor), new(new(rect.Right, rect.Top), fillColor),
            new(new(rect.Right, rect.Bottom), fillColor), new(new(rect.Left, rect.Bottom), fillColor),
        ];
        ushort[] indices = [0, 1, 2, 0, 2, 3];

        _cl.UpdateBuffer(_flatVertexBuffer, 0, vertices);
        _cl.UpdateBuffer(_flatIndexBuffer, 0, indices);

        _cl.SetPipeline(_flatPipeline);
        _cl.SetVertexBuffer(0, _flatVertexBuffer);
        _cl.SetIndexBuffer(_flatIndexBuffer, IndexFormat.UInt16);
        _cl.SetGraphicsResourceSet(0, _projMatrixSet);
        _cl.DrawIndexed((uint)indices.Length, 1, 0, 0, 0);
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        var fontAtlas = _fontManager.GetAtlas(style.FontName, style.FontSize);
        var measuredSize = fontAtlas.MeasureText(text);
        Vector2 textDrawPos = origin;

        // Alignment
        if (maxSize.X > 0)
        {
            if (alignment.Horizontal == HAlignment.Center) textDrawPos.X += (maxSize.X - measuredSize.X) / 2f;
            else if (alignment.Horizontal == HAlignment.Right) textDrawPos.X += maxSize.X - measuredSize.X;
        }
        if (maxSize.Y > 0)
        {
            if (alignment.Vertical == VAlignment.Center) textDrawPos.Y += (maxSize.Y - measuredSize.Y) / 2f;
            else if (alignment.Vertical == VAlignment.Bottom) textDrawPos.Y += maxSize.Y - measuredSize.Y;
        }

        List<VertexPositionTextureColor> vertices = new();
        List<ushort> indices = new();
        float currentX = textDrawPos.X;
        Color4 vertColor = color;
        ushort indexOffset = 0;

        foreach (char c in text)
        {
            if (!fontAtlas.Glyphs.TryGetValue(c, out var glyph)) continue;

            float x0 = currentX + glyph.Bearing.X;
            float y0 = textDrawPos.Y + glyph.Bearing.Y;
            float x1 = x0 + glyph.Size.X;
            float y1 = y0 + glyph.Size.Y;
            float u0 = glyph.SourceRect.Left;
            float v0 = glyph.SourceRect.Top;
            float u1 = glyph.SourceRect.Right;
            float v1 = glyph.SourceRect.Bottom;

            vertices.Add(new VertexPositionTextureColor(new(x0, y0), new(u0, v0), vertColor));
            vertices.Add(new VertexPositionTextureColor(new(x1, y0), new(u1, v0), vertColor));
            vertices.Add(new VertexPositionTextureColor(new(x1, y1), new(u1, v1), vertColor));
            vertices.Add(new VertexPositionTextureColor(new(x0, y1), new(u0, v1), vertColor));

            indices.Add((ushort)(indexOffset + 0));
            indices.Add((ushort)(indexOffset + 1));
            indices.Add((ushort)(indexOffset + 2));
            indices.Add((ushort)(indexOffset + 0));
            indices.Add((ushort)(indexOffset + 2));
            indices.Add((ushort)(indexOffset + 3));
            indexOffset += 4;

            currentX += glyph.Advance;
        }

        if (vertices.Count == 0) return;

        _cl.UpdateBuffer(_textVertexBuffer, 0, vertices.ToArray());
        _cl.UpdateBuffer(_textIndexBuffer, 0, indices.ToArray());

        using var textureSet = _gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
            _textTextureLayout, fontAtlas.AtlasTexture, _gd.Aniso4xSampler));

        _cl.SetPipeline(_textPipeline);
        _cl.SetVertexBuffer(0, _textVertexBuffer);
        _cl.SetIndexBuffer(_textIndexBuffer, IndexFormat.UInt16);
        _cl.SetGraphicsResourceSet(0, _projMatrixSet);
        _cl.SetGraphicsResourceSet(1, textureSet);
        _cl.DrawIndexed((uint)indices.Count, 1, 0, 0, 0);
    }

    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
    {
        _cl.SetScissorRect(0, (uint)rect.X, (uint)rect.Y, (uint)rect.Width, (uint)rect.Height);
    }

    public void PopClipRect()
    {
        _cl.SetScissorRect(0, 0, 0, (uint)RenderTargetSize.X, (uint)RenderTargetSize.Y);
    }

    public void Dispose()
    {
        Cleanup();
    }

    public void Cleanup()
    {
        _flatPipeline.Dispose();
        _textPipeline.Dispose();

        _projMatrixLayout.Dispose();
        _textTextureLayout.Dispose();

        _projMatrixSet.Dispose();

        _projMatrixBuffer.Dispose();
        _flatVertexBuffer.Dispose();
        _flatIndexBuffer.Dispose();
        _textVertexBuffer.Dispose();
        _textIndexBuffer.Dispose();
    }
}