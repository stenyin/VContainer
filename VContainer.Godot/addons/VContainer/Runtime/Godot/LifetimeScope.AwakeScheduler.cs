using System;
using System.Collections.Generic;
using Godot;

namespace VContainer.Godot;

public sealed class VContainerParentTypeReferenceNotFound : Exception
{
	public readonly Type ParentType;

	public VContainerParentTypeReferenceNotFound(Type parentType, string message) : base(message)
	{
		ParentType = parentType;
	}
}
