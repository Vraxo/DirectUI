using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DirectUI.Core;
using Veldrid;
using Vortice.Direct2D1;
using Rect = Vortice.Mathematics.Rect;

namespace DirectUI.Backends.Vulkan;

public class VeldridRenderer : IRenderer
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

        // Create resources
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

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        // Placeholder: a full implementation needs a font atlas texture, a different pipeline/shader,
        // and logic to generate vertices for each character quad.
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
        _pipeline.Dispose();
        _projMatrixLayout.Dispose();
        _projMatrixSet.Dispose();
        _projMatrixBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }
}