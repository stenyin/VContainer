using Godot;
using System;
using System.Collections.Generic;
using VContainer.Godot;
using VContainer.Internal;
using System.Linq;

#if TOOLS
namespace VContainer.Editor;

public partial class ParentReferenceEditorProperty : EditorProperty
{
	static string[] GetAllTypeNames()
	{
		return new List<string> { "None" }.Concat(TypeCache.GetTypesDerivedFrom<LifetimeScope>().Select(type => type.FullName)).ToArray();
	}

	static string GetLabel(Type type) => $"{type.Namespace}/{type.Name}";
	
	string[] names;
	private OptionButton optionButton = new OptionButton();

	public ParentReferenceEditorProperty()
	{
		AddChild(optionButton);
		AddFocusable(optionButton);
	}
	
	public override void _UpdateProperty()
	{
		if (names == null)
		{
			names = GetAllTypeNames();
			var scope = GetEditedObject() as LifetimeScope;
			var lifetimeScopeName = scope.GetType().FullName;
			names = names.Where(name => name != lifetimeScopeName).ToArray();
		}
		
		optionButton.Clear();
		foreach (var name in names)
		{
			optionButton.AddItem(name);
		}
		
		var referenceTypeName = (string) GetEditedObject().Get(GetEditedProperty());
		var index = Array.IndexOf(names, referenceTypeName);
		optionButton.Selected = index;
	}
}
#endif
