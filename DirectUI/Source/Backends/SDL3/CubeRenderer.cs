using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using SDL3;
using Vortice.Mathematics;
using static SDL3.SDL;
using Rect = Vortice.Mathematics.Rect;

namespace DirectUI.Backends.SDL3;

// A custom vertex structure for our cube
[StructLayout(LayoutKind.Sequential)]
public struct CubeVertex
{
    public Vector3 Position;
    public Color4 Color;

    public CubeVertex(Vector3 position, Color4 color)
    {
        Position = position;
        Color = color;
    }
}

public unsafe class CubeRenderer
{
    private readonly GPU_Device* _device;
    private GPU_Shader* _vertexShader;
    private GPU_Shader* _fragmentShader;
    private GPU_Pipeline* _pipeline;
    private GPU_Buffer* _vertexBuffer;
    private GPU_Buffer* _indexBuffer;
    private GCHandle _mvpGCHandle;
    private Matrix4x4 _mvpMatrix;

    private readonly Stopwatch _timer = new();
    private float _rotationY = 0.0f;

    public CubeRenderer(nint device)
    {
        _device = device;
    }

    public bool Initialize()
    {
        // 1. Create Shaders
        var vsBytes = Encoding.UTF8.GetBytes(Shaders.CubeVertexShader);
        var fsBytes = Encoding.UTF8.GetBytes(Shaders.CubeFragmentShader);

        fixed (byte* pVsBytes = vsBytes)
        fixed (byte* pFsBytes = fsBytes)
        {
            var vsCreateInfo = new GPU_ShaderCreateInfo
            {
                code = (nint)pVsBytes,
                codeSize = (uint)vsBytes.Length,
                format = GPUShaderFormat.Glsl,
                stage = GPUShaderStage.Vertex,
                entryPointName = "main"
            };
            _vertexShader = SDL.GpuCreateShader(_device, &vsCreateInfo);

            var fsCreateInfo = new GPU_ShaderCreateInfo
            {
                code = (nint)pFsBytes,
                codeSize = (uint)fsBytes.Length,
                format = GPUShaderFormat.Glsl,
                stage = GPUShaderStage.Fragment,
                entryPointName = "main"
            };
            _fragmentShader = SDL.GpuCreateShader(_device, &fsCreateInfo);
        }

        if (_vertexShader == null || _fragmentShader == null)
        {
            Console.WriteLine($"Failed to create shaders: {SDL.GetError()}");
            return false;
        }

        // 2. Create Buffers
        CreateCubeGeometry(out var vertices, out var indices);
        var vtxHandle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
        var idxHandle = GCHandle.Alloc(indices, GCHandleType.Pinned);

        var vtxBufCreateInfo = new GPU_BufferCreateInfo
        {
            size = (uint)(vertices.Length * sizeof(CubeVertex)),
            usageFlags = GPUBufferUsageFlags.Vertex
        };
        _vertexBuffer = SDL.GpuCreateBuffer(_device, &vtxBufCreateInfo);
        SDL.GpuTransferDataToBuffer(
            SDL.GpuAcquireCommandBuffer(_device),
            vtxHandle.AddrOfPinnedObject(),
            _vertexBuffer,
            0,
            vtxBufCreateInfo.size,
        true
        );

        var idxBufCreateInfo = new GPU_BufferCreateInfo
        {
            size = (uint)(indices.Length * sizeof(ushort)),
            usageFlags = GPUBufferUsageFlags.Index
        };
        _indexBuffer = SDL.GpuCreateBuffer(_device, &idxBufCreateInfo);
        SDL.GpuTransferDataToBuffer(
            SDL.GpuAcquireCommandBuffer(_device),
            idxHandle.AddrOfPinnedObject(),
            _indexBuffer,
            0,
            idxBufCreateInfo.size,
            true
        );

        vtxHandle.Free();
        idxHandle.Free();

        // 3. Create Pipeline
        var vertexBinding = new GPU_VertexBinding
        {
            binding = 0,
            stride = (uint)sizeof(CubeVertex),
            inputRate = GPUVertexInputRate.Vertex
        };

        GPU_VertexAttribute* attributes = (GPU_VertexAttribute*)NativeMemory.Alloc(2, (uint)sizeof(GPU_VertexAttribute));
        attributes[0] = new GPU_VertexAttribute
        {
            location = 0,
            binding = 0,
            format = GPUVertexElementFormat.Vector3,
            offset = (uint)Marshal.OffsetOf<CubeVertex>(nameof(CubeVertex.Position))
        };
        attributes[1] = new GPU_VertexAttribute
        {
            location = 1,
            binding = 0,
            format = GPUVertexElementFormat.Vector4,
            offset = (uint)Marshal.OffsetOf<CubeVertex>(nameof(CubeVertex.Color))
        };

        var vertexInputState = new GPU_VertexInputState
        {
            vertexBindingCount = 1,
            vertexBindings = &vertexBinding,
            vertexAttributeCount = 2,
            vertexAttributes = attributes
        };

        var shaders = stackalloc nint[2];
        shaders[0] = (nint)_vertexShader;
        shaders[1] = (nint)_fragmentShader;

        var colorAttachment = new GPU_ColorAttachmentDescription
        {
            format = SDL.GpuGetSwapchainTextureFormat(_device),
            blendState = new GPU_ColorBlendState() // Default opaque
        };

        var depthStencilState = new GPU_DepthStencilState
        {
            depthTestEnable = true,
            depthWriteEnable = true,
            depthCompareOp = GPUCompareOp.LessOrEqual
        };

        var pipelineCreateInfo = new GPU_GraphicsPipelineCreateInfo
        {
            vertexInputState = vertexInputState,
            shaderCount = 2,
            shaders = (nint*)shaders,
            primitiveType = GPUPrimitiveType.TriangleList,
            rasterizerState = new GPU_RasterizerState { cullMode = GPUCullMode.Back },
            multisampleState = new GPU_MultisampleState(),
            depthStencilState = depthStencilState,
            colorAttachmentCount = 1,
            colorAttachmentDescriptions = &colorAttachment,
            depthStencilFormat = GPUTextureFormat.D16Unorm,
        };

        _pipeline = SDL.GpuCreateGraphicsPipeline(_device, &pipelineCreateInfo);
        NativeMemory.Free(attributes);

        if (_pipeline == null)
        {
            Console.WriteLine($"Failed to create graphics pipeline: {SDL.GetError()}");
            return false;
        }

        _mvpMatrix = Matrix4x4.Identity;
        _mvpGCHandle = GCHandle.Alloc(_mvpMatrix, GCHandleType.Pinned);

        _timer.Start();
        return true;
    }

    public void Draw(GPU_CommandBuffer* cmdbuf, Rect viewport)
    {
        // Update MVP Matrix for rotation
        float elapsedSeconds = (float)_timer.Elapsed.TotalSeconds;
        _rotationY += 45.0f * elapsedSeconds;
        _timer.Restart();

        var world = Matrix4x4.CreateRotationY(MathHelper.ToRadians(_rotationY));
        var view = Matrix4x4.CreateLookAtLH(new Vector3(0, 2, -5), Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfViewLH(MathHelper.ToRadians(70f), viewport.Width / viewport.Height, 0.1f, 100f);
        _mvpMatrix = world * view * proj;

        // Pin the matrix to get its address
        _mvpGCHandle.Target = _mvpMatrix;

        var viewportGpu = new GPU_Viewport
        {
            x = viewport.X,
            y = viewport.Y,
            w = viewport.Width,
            h = viewport.Height
        };

        SDL.GpuSetViewport(cmdbuf, &viewportGpu);
        SDL.GpuBindGraphicsPipeline(cmdbuf, _pipeline);
        SDL.GpuPushGraphicsUniformData(cmdbuf, 0, _mvpGCHandle.AddrOfPinnedObject(), (uint)sizeof(Matrix4x4));

        var vertexBuffBinding = new GPU_BufferBinding { buffer = _vertexBuffer, offset = 0 };
        SDL.GpuBindVertexBuffers(cmdbuf, 0, &vertexBuffBinding, 1);
        SDL.GpuBindIndexBuffer(cmdbuf, new GPU_BufferBinding { buffer = _indexBuffer, offset = 0 }, GPUIndexElementSize.Sixteen);

        SDL.GpuDrawIndexedPrimitives(cmdbuf, 36, 1, 0, 0, 0);
    }

    public void Cleanup()
    {
        if (_mvpGCHandle.IsAllocated)
            _mvpGCHandle.Free();

        if (_pipeline != null) SDL.GpuDestroyPipeline(_device, _pipeline);
        if (_vertexBuffer != null) SDL.GpuDestroyBuffer(_device, _vertexBuffer);
        if (_indexBuffer != null) SDL.GpuDestroyBuffer(_device, _indexBuffer);
        if (_vertexShader != null) SDL.GpuDestroyShader(_device, _vertexShader);
        if (_fragmentShader != null) SDL.GpuDestroyShader(_device, _fragmentShader);
    }

    private void CreateCubeGeometry(out CubeVertex[] vertices, out ushort[] indices)
    {
        vertices =
        [
            // Front Face
            new CubeVertex(new Vector3(-1.0f, -1.0f,  1.0f), Colors.Red),
            new CubeVertex(new Vector3( 1.0f, -1.0f,  1.0f), Colors.Red),
            new CubeVertex(new Vector3( 1.0f,  1.0f,  1.0f), Colors.Red),
            new CubeVertex(new Vector3(-1.0f,  1.0f,  1.0f), Colors.Red),

            // Back Face
            new CubeVertex(new Vector3(-1.0f, -1.0f, -1.0f), Colors.Green),
            new CubeVertex(new Vector3(-1.0f,  1.0f, -1.0f), Colors.Green),
            new CubeVertex(new Vector3( 1.0f,  1.0f, -1.0f), Colors.Green),
            new CubeVertex(new Vector3( 1.0f, -1.0f, -1.0f), Colors.Green),
            
            // Top Face
            new CubeVertex(new Vector3(-1.0f, 1.0f, -1.0f), Colors.Blue),
            new CubeVertex(new Vector3(-1.0f, 1.0f,  1.0f), Colors.Blue),
            new CubeVertex(new Vector3( 1.0f, 1.0f,  1.0f), Colors.Blue),
            new CubeVertex(new Vector3( 1.0f, 1.0f, -1.0f), Colors.Blue),

            // Bottom Face
            new CubeVertex(new Vector3(-1.0f, -1.0f, -1.0f), Colors.Yellow),
            new CubeVertex(new Vector3( 1.0f, -1.0f, -1.0f), Colors.Yellow),
            new CubeVertex(new Vector3( 1.0f, -1.0f,  1.0f), Colors.Yellow),
            new CubeVertex(new Vector3(-1.0f, -1.0f,  1.0f), Colors.Yellow),

            // Right Face
            new CubeVertex(new Vector3(1.0f, -1.0f, -1.0f), Colors.Cyan),
            new CubeVertex(new Vector3(1.0f,  1.0f, -1.0f), Colors.Cyan),
            new CubeVertex(new Vector3(1.0f,  1.0f,  1.0f), Colors.Cyan),
            new CubeVertex(new Vector3(1.0f, -1.0f,  1.0f), Colors.Cyan),

            // Left Face
            new CubeVertex(new Vector3(-1.0f, -1.0f, -1.0f), Colors.Magenta),
            new CubeVertex(new Vector3(-1.0f, -1.0f,  1.0f), Colors.Magenta),
            new CubeVertex(new Vector3(-1.0f,  1.0f,  1.0f), Colors.Magenta),
            new CubeVertex(new Vector3(-1.0f,  1.0f, -1.0f), Colors.Magenta),
        ];

        indices =
        [
            // Front
            0, 1, 2, 0, 2, 3,
            // Back
            4, 5, 6, 4, 6, 7,
            // Top
            8, 9, 10, 8, 10, 11,
            // Bottom
            12, 13, 14, 12, 14, 15,
            // Right
            16, 17, 18, 16, 18, 19,
            // Left
            20, 21, 22, 20, 22, 23,
        ];
    }

    internal void Draw(nint cmdbuf, Rect rect)
    {
        throw new NotImplementedException();
    }
}