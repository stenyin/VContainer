using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace VContainer.Godot;

[GlobalClass, GodotClassName("LifetimeScope")]
public partial class LifetimeScope : Node
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
                GlobalExtraInstallers.Push(installer);
        }

        void IDisposable.Dispose()
        {
            lock (SyncRoot)
                GlobalExtraInstallers.Pop();
        }
    }

    static readonly Stack<LifetimeScope> GlobalOverrideParents = new Stack<LifetimeScope>();
    static readonly Stack<IInstaller> GlobalExtraInstallers = new Stack<IInstaller>();

    protected static RootLiftScope Root { get; set; }
    static readonly object SyncRoot = new();
    bool _isDisposed;

    [Export]
    public string ParentTypeName
    {
        get => parentReference.TypeName;
        set => parentReference.TypeName = value;
    }

    [Export] bool autoRun = true;
    [Export] Node[] autoInjectGameObjects = [];

    string scopeName;
    ParentReference parentReference;

    static LifetimeScope Create(IInstaller installer = null)
    {
        var node = new LifetimeScope();
        node.SetName("LifetimeScope");
        node.localExtraInstallers.Add(installer);
        Root.AddChild(node);
        return node;
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

        Array<Node> rootChildren = Root.GetChildren(true);
        foreach (Node child in rootChildren)
        {
            if (child.GetType() == type)
            {
                return child as LifetimeScope;
            }
        }

        Array<Node> childArray = scene.CurrentScene.GetChildren();
        if (scene.CurrentScene is LifetimeScope lifetimeScope && lifetimeScope.GetType() == type)
        {
            return lifetimeScope;
        }

        foreach (Node child in childArray)
        {
            if (child.GetType() == type)
            {
                return child as LifetimeScope;
            }
        }

        return null;
    }

    static LifetimeScope Find(Type type) => Find(type, Root.GetTree());

    readonly List<IInstaller> localExtraInstallers = [];
    public IObjectResolver Container { get; private set; }
    public LifetimeScope Parent { get; private set; }
    public bool IsRoot => this == Root;
    public Type ParentType => parentReference.Type;

    public LifetimeScope()
    {
        parentReference = new ParentReference()
        {
            OwnerType = GetType(),
            TypeName = "None"
        };
    }

    public override void _EnterTree()
    {
	    if (Engine.IsEditorHint())
	    {
		    return;
	    }
	    
        try
        {
            if (!IsRoot)
            {
                Parent = GetRuntimeParent();
            }

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
    
    public sealed override void _ExitTree()
    {
	    if (Engine.IsEditorHint())
	    {
		    return;
	    }
	    
        DisposeCore();
    }

    protected virtual void Configure(IContainerBuilder builder) { }
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        DisposeCore();
    }

    void DisposeCore()
    {
	    if(_isDisposed) return;
	    _isDisposed = true;
	    
        Container?.Dispose();
        Container = null;
        RootLiftScope.CancelReady(this);
    }

    public void Build()
    {
        if (!IsRoot)
        {
            Parent ??= GetRuntimeParent();
        }

        if (Parent != null)
        {
            if (Parent.IsRoot && Parent.Container == null)
            {
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
        AddChild(child);
        return child;
    }

    public LifetimeScope CreateChild(IInstaller installer = null) => CreateChild<LifetimeScope>(installer);

    public TScope CreateChild<TScope>(Action<IContainerBuilder> installation) where TScope : LifetimeScope, new()
        => CreateChild<TScope>(new ActionInstaller(installation));

    public LifetimeScope CreateChild(Action<IContainerBuilder> installation) => CreateChild<LifetimeScope>(new ActionInstaller(installation));

    public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, IInstaller installer = null) where TScope : LifetimeScope
    {
        Node sceneNode = scene.Instantiate();
        var child = sceneNode.GetChildren().FirstOrDefault() as TScope;
        if (child == null)
        {
            GD.PushWarning($"PackedScene {scene.ResourcePath} does not contain a {typeof(TScope).Name}.");
            return null;
        }

        if (installer != null)
        {
            child.localExtraInstallers.Add(installer);
        }

        child.parentReference.Object = this;
        AddChild(child);
        return child;
    }

    public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, Action<IContainerBuilder> installation) where TScope : LifetimeScope
        => CreateChildFromPackedScene<TScope>(scene, new ActionInstaller(installation));

    void InstallTo(IContainerBuilder builder)
    {
        Configure(builder);

        foreach (IInstaller installer in localExtraInstallers)
        {
            installer.Install(builder);
        }

        localExtraInstallers.Clear();

        lock (SyncRoot)
        {
            foreach (IInstaller installer in GlobalExtraInstallers)
            {
                installer.Install(builder);
            }
        }

        builder.RegisterInstance(this).AsSelf();
        EntryPointsBuilder.EnsureDispatcherRegistered(builder);
    }

    protected virtual LifetimeScope FindParent() => null;

    LifetimeScope GetRuntimeParent()
    {
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
            if (Find(parentReference.Type) is { Container: not null } foundScope)
                return foundScope;

            throw new VContainerParentTypeReferenceNotFound(parentReference.Type, $"{Name} could not found parent reference of type : {parentReference.Type}");
        }

        lock (SyncRoot)
        {
            if (GlobalOverrideParents.Count > 0)
            {
                return GlobalOverrideParents.Peek();
            }
        }

        if (parentReference.Type != null && parentReference.Type != GetType())
        {
            if (Find(parentReference.Type) is { Container: not null } foundScope)
                return foundScope;

            throw new VContainerParentTypeReferenceNotFound(parentReference.Type, $"{Name} could not found parent reference of type : {parentReference.Type}");
        }

        if (parentReference.Type != null)
        {
            GD.PushWarning("Parent reference cannot be same as self.");
        }

        parentReference = ParentReference.Create<RootLiftScope>(GetType());
        return null;
    }

    void AutoInjectAll()
    {
        if (autoInjectGameObjects == null)
            return;

        foreach (Node target in autoInjectGameObjects)
        {
            if (target != null) // Check missing reference
            {
                Container.InjectNode(target);
            }
        }
    }

    public void SetParent(LifetimeScope awakenParent)
    {
        if (parentReference.Type != awakenParent.GetType())
            return;

        parentReference.Object = awakenParent;
    }
}