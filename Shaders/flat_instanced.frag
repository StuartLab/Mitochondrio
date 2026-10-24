#version 330 core

in vec3 v_colour;
out vec4 out_color;

void main()
{
    out_color = vec4(v_colour, 1.0);
}
