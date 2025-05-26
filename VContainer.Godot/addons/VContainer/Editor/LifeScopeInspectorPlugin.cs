using Godot;

#if TOOLS
namespace VContainer.Godot.Editor;

public partial class LifeScopeInspectorPlugin : EditorInspectorPlugin
{
	public override bool _CanHandle(GodotObject @object)
	{
		if (@object is LifetimeScope)
		{
			return true;
		}

		if (@object is not Node node || node.GetScript().As<CSharpScript>() is not { } cSharpScript)
		{
			return false;
		}

		while (cSharpScript != null)
		{
			if (cSharpScript.GetGlobalName().ToString() is "LifetimeScope")
			{
				return true;
			}

			cSharpScript = cSharpScript.GetBaseScript() as CSharpScript;
		}

		return false;
	}

	public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
	{
		if (type == Variant.Type.String && name == "ParentTypeName")
		{
			AddPropertyEditor(name, new ParentReferenceEditorProperty());
			return true;
		}

		return false;
	}
}
#endif