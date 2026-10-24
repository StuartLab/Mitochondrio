#version 330 core

in vec2 v_uv;
out vec4 out_color;

uniform sampler2D srcTex;
uniform int prefilter;      // 1 for the first step
uniform float threshold;    // brightness (largest colour channel) where glow starts
uniform float knee;         // softness of that cut-off

// determines which parts glow, applies only to first step because further downsamples can just reuse the glow of step 1
vec3 Prefilter(vec3 c)
{
    if (prefilter == 0) return c;

    float brightness = max(c.r, max(c.g, c.b));
    float soft = clamp(brightness - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-5);
    float contribution = max(soft, brightness - threshold) / max(brightness, 1e-5);
    return c * contribution;
}

vec3 S(vec2 offset)
{
    return Prefilter(texture(srcTex, v_uv + offset).rgb);
}

// 13 tap downsample to eliminate artifacts
void main()
{
    vec2 t = 1.0 / vec2(textureSize(srcTex, 0));

    vec3 a = S(t * vec2(-2.0,  2.0));
    vec3 b = S(t * vec2( 0.0,  2.0));
    vec3 c = S(t * vec2( 2.0,  2.0));
    vec3 d = S(t * vec2(-2.0,  0.0));
    vec3 e = S(vec2(0.0));
    vec3 f = S(t * vec2( 2.0,  0.0));
    vec3 g = S(t * vec2(-2.0, -2.0));
    vec3 h = S(t * vec2( 0.0, -2.0));
    vec3 i = S(t * vec2( 2.0, -2.0));
    vec3 j = S(t * vec2(-1.0,  1.0));
    vec3 k = S(t * vec2( 1.0,  1.0));
    vec3 l = S(t * vec2(-1.0, -1.0));
    vec3 m = S(t * vec2( 1.0, -1.0));

    vec3 result = e * 0.125
                + (a + c + g + i) * 0.03125
                + (b + d + f + h) * 0.0625
                + (j + k + l + m) * 0.125;

    out_color = vec4(result, 1.0);
}
