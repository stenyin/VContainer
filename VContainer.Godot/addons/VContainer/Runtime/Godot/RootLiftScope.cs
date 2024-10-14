using Godot;
using System.Collections.Generic;

namespace VContainer.Godot;

public partial class RootLiftScope : LifetimeScope
{
	private Window TreeRoot;
	private static RootLiftScope instance;

	public override void _EnterTree()
	{
		if (instance != null)
		{
			throw new System.InvalidOperationException("RootLiftScope is already instantiated. Do not instantiate it manually.");
		}

		Root = instance = this;
		TreeRoot = GetTree().Root;
		
		base._EnterTree();
		TreeRoot.ChildEnteredTree += OnChildEnteredTreeRoot;
	}
	
	public override void _ExitTree()
	{
		if (instance == this)
		{
			instance = null;
			Root = null;
		}
		TreeRoot.ChildEnteredTree -= OnChildEnteredTreeRoot;
		TreeRoot = null;
	}
    
	static readonly List<LifetimeScope> WaitingList = new List<LifetimeScope>();
	
	internal static bool WaitingListContains(LifetimeScope lifetimeScope)
	{
		return WaitingList.Contains(lifetimeScope);
	}

	internal static void EnqueueReady(LifetimeScope lifetimeScope)
	{
		WaitingList.Add(lifetimeScope);
	}

	internal static void CancelReady(LifetimeScope lifetimeScope)
	{
		WaitingList.Remove(lifetimeScope);
	}
	
    public static void ReadyWaitingChildren(LifetimeScope awakenParent)
    {
        if (WaitingList.Count <= 0) return;

        List<LifetimeScope> buffer = new ();
        for (var i = WaitingList.Count - 1; i >= 0; i--)
        {
            var waitingScope = WaitingList[i];
            if (waitingScope.parentReference.Type == awakenParent.GetType())
            {
                waitingScope.parentReference.Object = awakenParent;
                WaitingList.RemoveAt(i);
                buffer.Add(waitingScope);
            }
        }

        foreach (var waitingScope in buffer)
        {
            waitingScope.RequestReady();
        }
    }

    private static void OnChildEnteredTreeRoot(Node child)
    {
        // Ignore if child is not in the current scene
        if (child != child.GetTree().CurrentScene)
	        return;
        
        OnSceneChange(child);
    }
    
    private static void OnSceneChange(Node child)
	{
		if (WaitingList.Count <= 0)
			return;

		List<LifetimeScope> buffer = new ();
		for (var i = WaitingList.Count - 1; i >= 0; i--)
		{
			var waitingScope = WaitingList[i];
			if (child.GetTree().CurrentScene == waitingScope.GetTree().CurrentScene)
			{
				WaitingList.RemoveAt(i);
				buffer.Add(waitingScope);
			}
		}

		foreach (var waitingScope in buffer)
		{
			waitingScope._Ready(); // Re-throw if parent not found
		}
	}
}
