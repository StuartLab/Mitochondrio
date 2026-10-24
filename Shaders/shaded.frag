#version 330 core
in vec3 v_vert;
in vec3 v_norm;

out vec4 out_color;

uniform vec3 lightPos;
uniform vec3 viewPos;

void main()
{
    vec3 objectColor = vec3(0.8, 0.2, 0.8);
    vec3 lightColor = vec3(0.8, 0.8, 0.1);


    float ambientStrength = 0.1;
    vec3 ambient = ambientStrength * lightColor;

    vec3 norm = normalize(v_norm);
    vec3 lightDir = -normalize(lightPos - v_vert);

    float diff = max(dot(norm, lightDir), 0.2);
    vec3 diffuse = diff * lightColor;

    float specularStrength = 0.5;
    vec3 viewDir = normalize(viewPos - v_vert);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32); //The 32 is the shininess of the material
    vec3 specular = specularStrength * spec * lightColor;

    vec3 result = (ambient + diffuse + specular) * objectColor;
    out_color = vec4(result, 1.0);

    //float lum = clamp(dot(normalize(lightPos-v_vert), normalize(v_norm)), 0.0, 1.0) * 0.8 + 0.2;
    //out_color = vec4(1.0f, 0.5f, 0.2f, 1.0f);
}