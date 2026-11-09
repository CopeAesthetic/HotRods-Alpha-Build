shader_type spatial;
render_mode specular_schlick_ggx, diffuse_burley;

uniform vec3 shallow : source_color = vec3(0.30, 0.56, 0.56);
uniform vec3 deep : source_color = vec3(0.05, 0.20, 0.30);
uniform float wave_amp = 0.10;
uniform float time_scale = 1.0;

float wave(vec3 p, float t) {
    return sin(p.x * 0.30 + t * 1.30) * 0.45
         + sin(p.z * 0.42 + t * 1.60) * 0.30
         + sin((p.x + p.z) * 0.18 + t * 2.10) * 0.25;
}

void vertex() {
    vec3 p = VERTEX;
    float t = TIME * time_scale;
    float e = 0.9;
    float h = wave(p, t) * wave_amp;
    VERTEX.y += h;
    float hx = wave(p + vec3(e, 0.0, 0.0), t) * wave_amp;
    float hz = wave(p + vec3(0.0, 0.0, e), t) * wave_amp;
    vec3 tangent = normalize(vec3(e, hx - h, 0.0));
    vec3 bitangent = normalize(vec3(0.0, hz - h, e));
    NORMAL = normalize(cross(bitangent, tangent));
}

void fragment() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float dist = length(world.xz - vec2(0.0, 13.0));
    float depth = clamp(dist / 32.0, 0.0, 1.0);
    ALBEDO = mix(shallow, deep, depth);
    ROUGHNESS = 0.04;
    METALLIC = 0.0;
    SPECULAR = 0.85;
    ALBEDO += vec3(0.06) * sin(world.x * 1.7 + world.z * 2.3 + TIME * 2.0)
            * sin(world.x * 0.9 - world.z * 1.4 + TIME * 1.4);
}
