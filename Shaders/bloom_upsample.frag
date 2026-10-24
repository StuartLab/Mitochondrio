#version 330 core

in vec2 v_uv;
out vec4 out_color;

uniform sampler2D srcTex;
uniform float radius;   // width of the blur, in texels of srcTex

// 3x3 tent filter
void main()
{
    vec2 t = radius / vec2(textureSize(srcTex, 0));

    vec3 a = texture(srcTex, v_uv + vec2(-t.x,  t.y)).rgb;
    vec3 b = texture(srcTex, v_uv + vec2( 0.0,  t.y)).rgb;
    vec3 c = texture(srcTex, v_uv + vec2( t.x,  t.y)).rgb;
    vec3 d = texture(srcTex, v_uv + vec2(-t.x,  0.0)).rgb;
    vec3 e = texture(srcTex, v_uv).rgb;
    vec3 f = texture(srcTex, v_uv + vec2( t.x,  0.0)).rgb;
    vec3 g = texture(srcTex, v_uv + vec2(-t.x, -t.y)).rgb;
    vec3 h = texture(srcTex, v_uv + vec2( 0.0, -t.y)).rgb;
    vec3 i = texture(srcTex, v_uv + vec2( t.x, -t.y)).rgb;

    // source texel x4, orthogonal texels x2, diagonal texels x1
    vec3 result = e * 4.0 + (b + d + f + h) * 2.0 + (a + c + g + i);
    // division by 16 to match the multiplied values above
    out_color = vec4(result / 16.0, 1.0);
}
