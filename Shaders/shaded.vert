#version 330 core
layout(location = 0) in vec3 in_vert;
layout(location = 1) in vec2 in_tex;
layout(location = 2) in vec3 in_norm;

out vec3 v_vert;
out vec3 v_norm;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    v_vert = in_vert;
    v_norm = in_norm;

    gl_Position = vec4(in_vert, 1.0) * model * view * projection;
}