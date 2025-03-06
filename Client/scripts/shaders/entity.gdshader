shader_type canvas_item;

uniform vec4 color : source_color = vec4(1.0);
uniform vec4 modulate : source_color = vec4(1.0);
uniform float width : hint_range(0, 50) = 1.0;
uniform int pattern : hint_range(0, 2) = 0; // diamond, circle, square
uniform bool inside = false;

bool hasContraryNeighbour(vec2 uv, vec2 texture_pixel_size, sampler2D texture) {
	for (float i = -ceil(width); i <= ceil(width); i++) {
		float x = abs(i) > width ? width * sign(i) : i;
		float offset;
		
		if (pattern == 0) {
			offset = width - abs(x);
		} else if (pattern == 1) {
			offset = floor(sqrt(pow(width + 0.5, 2) - x * x));
		} else if (pattern == 2) {
			offset = width;
		}
		
		for (float j = -ceil(offset); j <= ceil(offset); j++) {
			float y = abs(j) > offset ? offset * sign(j) : j;
			vec2 xy = uv + texture_pixel_size * vec2(x, y);
			vec4 pixel = texture(texture, xy);
			if (distance(xy, vec2(0.5)) > 0.5){
				pixel.a = 0.0;
			}
			
			if ((xy != clamp(xy, vec2(0.0), vec2(1.0)) || pixel.a == 0.0) == inside) {
				return true;
			}
		}
	}
	
	return false;
}

void fragment() {
	vec2 uv = UV;
	bool masked_out = distance(uv, vec2(0.5)) > 0.5;

    COLOR = texture(TEXTURE, uv);
    if (masked_out){
        COLOR.a = 0.0;
    }
	
	if ((COLOR.a > 0.0) == inside && hasContraryNeighbour(uv, TEXTURE_PIXEL_SIZE, TEXTURE)) {
		COLOR.rgb = inside ? mix(COLOR.rgb, color.rgb, color.a) : color.rgb;
		COLOR.a += (1.0 - COLOR.a) * color.a;
	}

	COLOR *= modulate;
}
