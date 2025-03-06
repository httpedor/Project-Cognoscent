extends Control

@export var network_manager: Node
@export var game_manager: Node
var error_label: Node

# Called when the node enters the scene tree for the first time.
func _ready():
	var btn = get_child(1).get_child(4)
	var username_line = get_child(1).get_child(1)
	var ip_line = get_child(1).get_child(3)
	error_label = get_child(1).get_child(5)
	btn.connect("pressed", func():
		_on_connect(username_line.text, ip_line.text)
	)

func _on_connect(username: String, ip: String):
	var split = ip.split(":")
	if len(split) != 2:
		error_label.text = "Invalid IP"
		return
	var ip_address = ip.split(":")[0]
	var port = int(ip.split(":")[1])
	game_manager.Username = username
	var err = network_manager.ConnectToHost(ip_address, port)
	if err != OK:
		error_label.text = "Error connecting: " + error_string(err)
	else:
		queue_free()
