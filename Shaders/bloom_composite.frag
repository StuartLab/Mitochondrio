#version 330 core

in vec2 v_uv;
out vec4 out_color;

uniform sampler2D sceneTex;
uniform sampler2D bloomTex;
uniform float strength;

void main()
{
    vec3 scene = texture(sceneTex, v_uv).rgb;
    vec3 bloom = texture(bloomTex, v_uv).rgb;

    out_color = vec4(scene + bloom * strength, 1.0);
}