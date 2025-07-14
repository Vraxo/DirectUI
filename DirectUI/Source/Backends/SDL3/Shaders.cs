namespace DirectUI.Backends.SDL3;

public static class Shaders
{
    public const string CubeVertexShader = @"#version 450
layout(location = 0) in vec3 a_position;
layout(location = 1) in vec4 a_color;

layout(location = 0) out vec4 v_color;

layout(push_constant) uniform Uniforms
{
    mat4 mvp;
} ubo;

void main()
{
    gl_Position = ubo.mvp * vec4(a_position, 1.0);
    v_color = a_color;
}
";

    public const string CubeFragmentShader = @"#version 450
layout(location = 0) in vec4 v_color;

layout(location = 0) out vec4 o_color;

void main()
{
    o_color = v_color;
}
";
}