namespace DirectUI.Backends.Vulkan;

public static class VeldridShaders
{
    public const string FlatVertexShader = @"
#version 450
layout(location = 0) in vec2 a_position;
layout(location = 1) in vec4 a_color;

layout(set = 0, binding = 0) uniform ProjectionMatrix
{
    mat4 u_matrix;
};

layout(location = 0) out vec4 v_color;

void main()
{
    gl_Position = u_matrix * vec4(a_position, 0.0, 1.0);
    v_color = a_color;
}";

    public const string FlatFragmentShader = @"
#version 450
layout(location = 0) in vec4 v_color;
layout(location = 0) out vec4 o_color;

void main()
{
    o_color = v_color;
}";

    public const string TexturedVertexShader = @"
#version 450
layout(location = 0) in vec2 a_position;
layout(location = 1) in vec2 a_texCoord;
layout(location = 2) in vec4 a_color;

layout(set = 0, binding = 0) uniform ProjectionMatrix
{
    mat4 u_matrix;
};

layout(location = 0) out vec2 v_texCoord;
layout(location = 1) out vec4 v_color;

void main()
{
    gl_Position = u_matrix * vec4(a_position, 0.0, 1.0);
    v_texCoord = a_texCoord;
    v_color = a_color;
}";

    public const string TexturedFragmentShader = @"
#version 450
layout(location = 0) in vec2 v_texCoord;
layout(location = 1) in vec4 v_color;

layout(set = 1, binding = 0) uniform texture2D u_texture;
layout(set = 1, binding = 1) uniform sampler u_sampler;

layout(location = 0) out vec4 o_color;

void main()
{
    // Modulate texture alpha (from .r channel of R8 texture) with vertex color.
    float alpha = texture(sampler2D(u_texture, u_sampler), v_texCoord).r;
    o_color = vec4(v_color.rgb, v_color.a * alpha);
}";
}