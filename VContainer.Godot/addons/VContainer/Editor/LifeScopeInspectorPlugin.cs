using Godot;
using VContainer.Godot;

#if TOOLS
namespace VContainer.Editor;

public partial class LifeScopeInspectorPlugin: EditorInspectorPlugin
{
	public override bool _CanHandle(GodotObject @object)
	{
		return @object is LifetimeScope;
	}

	public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
	{
		if (type == Variant.Type.String && name == "parentTypeName")
		{
			AddPropertyEditor(name, new ParentReferenceEditorProperty());
			return true;
		}

		return false;
	}
}
#endif
