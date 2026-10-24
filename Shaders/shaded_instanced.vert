#version 330 core
layout(location = 0) in vec3 in_vert;
layout(location = 1) in vec2 in_tex;
layout(location = 2) in vec3 in_norm;
layout(location = 3) in mat4 in_inst;
layout(location = 7) in vec3 in_col;

out vec3 v_vert;
out vec3 v_norm;
out vec3 v_colour;

uniform mat4 view;
uniform mat4 projection;

void main()
{
    mat4 model = in_inst;

    v_vert = in_vert;
    v_norm = in_norm;
    v_colour = in_col;

    gl_Position = vec4(in_vert, 1.0) * model * view * projection;
}