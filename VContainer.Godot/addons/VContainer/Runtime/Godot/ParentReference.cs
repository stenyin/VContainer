using System;
using Godot;

namespace VContainer.Godot;

public partial struct ParentReference
{
	private string typeName;

	[Export]
	public string TypeName
	{
		get
		{
			OnBeforeSerialize();
			return typeName;
		}
		set
		{
			typeName = value;
			OnAfterDeserialize();
		}
	}

	public LifetimeScope Object;

	public Type Type { get; private set; }

	public ParentReference()
	{
		Type = null;
		typeName = null;
		Object = null;
	}
	
	ParentReference(Type type) : this()
	{
		Type = type;
		TypeName = type.FullName;
		Object = null;
	}

	private void OnBeforeSerialize()
	{
		TypeName = Type?.FullName;
	}

	public void OnAfterDeserialize()
	{
		if (!string.IsNullOrEmpty(TypeName))
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type = assembly.GetType(TypeName);
				if (Type != null)
					break;
			}
		}
	}

	public static ParentReference Create<T>() where T : LifetimeScope
	{
		return new ParentReference(typeof(T));
	}
	
	public static ParentReference Create(string typeName)
	{
		return new ParentReference(Type.GetType(typeName));
	}
}
