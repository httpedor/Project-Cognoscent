shader_type canvas_item;
render_mode unshaded;

uniform vec3 ambient_color : source_color;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec4 vision_pixel = texture(TEXTURE, UV);
	vision_pixel.rgb /= ambient_color; //remove ambient light
	float avr = (vision_pixel.r + vision_pixel.g + vision_pixel.b) / 3.0;
	COLOR.rgb = vec3(0, 0, 0);
	COLOR.a = avr;
}

//void light() {
	// Called for every pixel for every light affecting the CanvasItem.
	// Uncomment to replace the default light processing function with this one.
//}
