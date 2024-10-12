@tool
extends EditorPlugin

var plugin_instance

func _enter_tree():
	
	plugin_instance = preload("res://addons/VContainer/Editor/LifeScopeInspectorPlugin.cs").new()
	
	# When this plugin node enters tree, add the custom types.
	add_custom_type("LifetimeScope", "Node", preload("res://addons/VContainer/Runtime/Godot/LifetimeScope.cs"), null)
	add_autoload_singleton("RootLiftScope", "res://addons/VContainer/Runtime/Godot/RootLiftScope.cs")
	
	add_inspector_plugin(plugin_instance)
	

func _exit_tree():

	# When the plugin node exits the tree, remove the custom types.
	remove_custom_type("LifetimeScope")
	remove_autoload_singleton("RootLiftScope")
	remove_inspector_plugin(plugin_instance)
	
