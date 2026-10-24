#version 330 core

layout(location = 0) in vec3 in_vert;
layout(location = 3) in mat4 in_inst;   // uses locations 3-6
layout(location = 7) in vec3 in_col;

out vec3 v_colour;

uniform mat4 view;
uniform mat4 projection;

void main()
{
    v_colour = in_col;
    gl_Position = vec4(in_vert, 1.0) * in_inst * view * projection;
}
