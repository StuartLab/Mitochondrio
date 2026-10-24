#version 330 core

layout(location = 0) in vec3 in_pos;
layout(location = 1) in vec3 in_end;
layout(location = 2) in vec3 in_color;
layout(location = 3) in float in_thickness;
layout(location = 4) in float in_side;

uniform mat4 view;
uniform mat4 projection;
uniform vec3 cameraPos;

out vec3 v_color;

void main()
{
    vec3 lineDir = normalize(in_end - in_pos);
    
    vec3 toCamera = normalize(cameraPos - in_pos);
    
    // Billboard direction
    vec3 right = normalize(cross(lineDir, toCamera));
    
    // Offset the vertex perpendicular to the line
    vec3 offsetPos = in_pos + right * (in_thickness * 0.5) * in_side;
    
    gl_Position = vec4(offsetPos, 1.0) * view * projection;
    v_color = in_color;
}