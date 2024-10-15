using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace VContainer.Godot;

sealed class ExistingNodeProvider : IInstanceProvider
{
	readonly object instance;
	readonly IInjector injector;
	readonly IReadOnlyList<IInjectParameter> customParameters;
	readonly bool isRootObject;

	public ExistingNodeProvider(
		object instance,
		IInjector injector,
		IReadOnlyList<IInjectParameter> customParameters,
		bool isRootObject = false)
	{
		this.instance = instance;
		this.customParameters = customParameters;
		this.injector = injector;
		this.isRootObject = isRootObject;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public object SpawnInstance(IObjectResolver resolver)
	{
		injector.Inject(instance, resolver, customParameters);
		if (isRootObject)
		{
			if (instance is Node node)
			{
				node.GetTree().Root.AddChild(node);
			}
			else
			{
				throw new VContainerException(instance.GetType(),
					$"Cannot apply `DontDestroyOnLoad`. {instance.GetType().Name} is not a UnityEngine.Object");
			}
		}
		return instance;
	}
}
