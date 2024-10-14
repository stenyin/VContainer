using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VContainer.Godot;

public partial class LifetimeScope : Node, IDisposable
{
	public readonly struct ParentOverrideScope : IDisposable
	{
		public ParentOverrideScope(LifetimeScope nextParent)
		{
			lock (SyncRoot)
			{
				GlobalOverrideParents.Push(nextParent);
			}
		}

		public void Dispose()
		{
			lock (SyncRoot)
			{
				GlobalOverrideParents.Pop();
			}
		}
	}

	public readonly struct ExtraInstallationScope : IDisposable
	{
		public ExtraInstallationScope(IInstaller installer)
		{
			lock (SyncRoot)
			{
				GlobalExtraInstallers.Push(installer);
			}
		}

		void IDisposable.Dispose()
		{
			lock (SyncRoot)
			{
				GlobalExtraInstallers.Pop();
			}
		}
	}

	public ParentReference parentReference;

	[Export] public string parentTypeName
	{
		get => parentReference.TypeName;
		set => parentReference.TypeName = value;
	}

	[Export] public bool autoRun = true;
	[Export] protected Node[] autoInjectGameObjects = Array.Empty<Node>();
	string scopeName;
	
	static readonly Stack<LifetimeScope> GlobalOverrideParents = new Stack<LifetimeScope>();
	static readonly Stack<IInstaller> GlobalExtraInstallers = new Stack<IInstaller>();
	static readonly object SyncRoot = new object();

	static LifetimeScope Create(IInstaller installer = null)
	{
		var node = new LifetimeScope();
		node.SetName("LifetimeScope");
		var variant = node.GetScript();
		if (variant.Obj is LifetimeScope newScope)
		{
			newScope.localExtraInstallers.Add(installer);
		}
		else
		{
			newScope = null;
		}
		Root.AddChild(node);
		return newScope;
	}

	public static LifetimeScope Create(Action<IContainerBuilder> configuration) => Create(new ActionInstaller(configuration));
	public static ParentOverrideScope EnqueueParent(LifetimeScope parent) => new ParentOverrideScope(parent);
	public static ExtraInstallationScope Enqueue(Action<IContainerBuilder> installing) => new ExtraInstallationScope(new ActionInstaller(installing));
	public static ExtraInstallationScope Enqueue(IInstaller installer) => new ExtraInstallationScope(installer);
	public static LifetimeScope Find<T>(SceneTree scene) where T : LifetimeScope => Find(typeof(T), scene);
	public static LifetimeScope Find<T>() where T : LifetimeScope => Find(typeof(T));

	static LifetimeScope Find(Type type, SceneTree scene)
	{
		if (type == typeof(RootLiftScope))
		{
			return Root;
		}

		var rootChildren = Root.GetChildren(true);
		foreach (var child in rootChildren)
		{
			if (child.GetType() == type)
			{
				return child as LifetimeScope;
			}
		}

		var childArray = scene.CurrentScene.GetChildren();
		foreach (var node in childArray)
		{
			var enumerable = node.GetChildren().Where(node => node.GetType() == type);
			if (enumerable.Any())
			{
				return enumerable.First() as LifetimeScope;
			}
		}

		return null;
	}

	static LifetimeScope Find(Type type)
	{
		return Find(type, Root.GetTree());
	}

	public static RootLiftScope Root { get; protected set; }
	public IObjectResolver Container { get; private set; }
	public LifetimeScope Parent { get; private set; }

	public bool IsRoot => this == Root;

	readonly List<IInstaller> localExtraInstallers = new List<IInstaller>();

	public LifetimeScope() : base()
	{
		parentReference = new ParentReference()
		{
			OwnerType = GetType()
		};
	}
	
	public override void _EnterTree()
	{
		try
		{
			Parent = GetRuntimeParent();
			if (autoRun)
			{
				Build();
			}
		}
		catch (VContainerParentTypeReferenceNotFound) when (!IsRoot)
		{
			if (RootLiftScope.WaitingListContains(this))
			{
				throw;
			}

			RootLiftScope.EnqueueReady(this);
		}
	}

	protected virtual void OnDestroy()
	{
		DisposeCore();
	}

	protected virtual void Configure(IContainerBuilder builder) { }

	public new void Dispose(bool disposing)
	{
		DisposeCore();
		if (this != null)
		{
			QueueFree();
		}
	}

	public void DisposeCore()
	{
		Container?.Dispose();
		Container = null;
		RootLiftScope.CancelReady(this);
	}

	public void Build()
	{
		if (Parent == null)
			Parent = GetRuntimeParent();

		if (Parent != null)
		{
			if (Parent.IsRoot)
			{
				if (Parent.Container == null)
					Parent.Build();
			}

			// ReSharper disable once PossibleNullReferenceException
			Parent.Container.CreateScope(builder =>
			{
				builder.RegisterBuildCallback(SetContainer);
				builder.ApplicationOrigin = this;
				builder.Diagnostics = null; // TODO: DiagnosticsContext.GetCollector(scopeName),
				InstallTo(builder);
			});
		}
		else
		{
			var builder = new ContainerBuilder
			{
				ApplicationOrigin = this,
				Diagnostics = null, // TODO: DiagnosticsContext.GetCollector(scopeName),
			};

			builder.RegisterBuildCallback(SetContainer);
			InstallTo(builder);
			builder.Build();
		}

		RootLiftScope.ReadyWaitingChildren(this);
	}

	void SetContainer(IObjectResolver container)
	{
		Container = container;
		AutoInjectAll();
	}


	public TScope CreateChild<TScope>(IInstaller installer = null) where TScope : LifetimeScope, new()
	{
		var child = new TScope();
		child.SetName("LifetimeScope (Child)");
		if (installer != null)
		{
			child.localExtraInstallers.Add(installer);
		}
		child.parentReference.Object = this;
		this.AddChild(child);
		return child;
	}

	public LifetimeScope CreateChild(IInstaller installer = null) => CreateChild<LifetimeScope>(installer);

	public TScope CreateChild<TScope>(Action<IContainerBuilder> installation) where TScope : LifetimeScope, new()
		=> CreateChild<TScope>(new ActionInstaller(installation));

	public LifetimeScope CreateChild(Action<IContainerBuilder> installation) => CreateChild<LifetimeScope>(new ActionInstaller(installation));

	public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, IInstaller installer = null) where TScope : LifetimeScope
	{
		var sceneNode = scene.Instantiate();
		var child = sceneNode.GetChildren().FirstOrDefault() as TScope;
		if (installer != null)
		{
			child.localExtraInstallers.Add(installer);
		}
		child.parentReference.Object = this;
		this.AddChild(child);
		return child;
	}

	public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, Action<IContainerBuilder> installation) where TScope : LifetimeScope
		=> CreateChildFromPackedScene<TScope>(scene, new ActionInstaller(installation));

	void InstallTo(IContainerBuilder builder)
	{
		Configure(builder);

		foreach (var installer in localExtraInstallers)
		{
			installer.Install(builder);
		}
		localExtraInstallers.Clear();

		lock (SyncRoot)
		{
			foreach (var installer in GlobalExtraInstallers)
			{
				installer.Install(builder);
			}
		}

		builder.RegisterInstance<LifetimeScope>(this).AsSelf();
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
	}
	
	protected virtual LifetimeScope FindParent() => null;

	LifetimeScope GetRuntimeParent()
	{
		if (IsRoot) return null;
		
		if (parentReference.Type == null)
		{
			parentReference = ParentReference.Create<RootLiftScope>(GetType());
		}
		
		if(parentReference.Type == GetType())
		{
			GD.PushWarning("Parent reference cannot be same as self.");
			parentReference = ParentReference.Create<RootLiftScope>(GetType());
		}

		if (parentReference.Object != null)
			return parentReference.Object;

		// Find via implementation
		var implParent = FindParent();
		if (implParent != null)
		{
			if (parentReference.Type != null && parentReference.Type != implParent.GetType())
			{
				GD.PushWarning($"FindParent returned {implParent.GetType()} but parent parentReference type is {parentReference.Type}. This may be unintentional.");
			}
			return implParent;
		}

		// Find in scene via type
		if (parentReference.Type != null && parentReference.Type != GetType())
		{
			var foundScope = Find(parentReference.Type);
			if (foundScope != null && foundScope.Container != null)
			{
				return foundScope;
			}
			throw new VContainerParentTypeReferenceNotFound(parentReference.Type, $"{Name} could not found parent reference of type : {parentReference.Type}");
		}

		lock (SyncRoot)
		{
			if (GlobalOverrideParents.Count > 0)
			{
				return GlobalOverrideParents.Peek();
			}
		}
		
		return null;
	}

	void AutoInjectAll()
	{
		if (autoInjectGameObjects == null)
			return;

		foreach (var target in autoInjectGameObjects)
		{
			if (target != null) // Check missing reference
			{
				Container.InjectNode(target);
			}
		}
	}
}
