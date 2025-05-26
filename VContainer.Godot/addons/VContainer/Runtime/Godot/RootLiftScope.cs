using Godot;
using System.Collections.Generic;

namespace VContainer.Godot;

public partial class RootLiftScope : LifetimeScope
{
	private Window treeRoot;

	public RootLiftScope()
	{
		if (Root != null)
		{
			throw new System.InvalidOperationException("RootLiftScope is already instantiated. Do not instantiate it manually.");
		}

		Root = this;
	}

	public override void _EnterTree()
	{
		treeRoot = GetTree().Root;
		treeRoot.ChildEnteredTree += OnChildEnteredTreeRoot;
		base._EnterTree();
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		if (!disposing)
			return;

		if (Root == this)
			Root = null;

		if (treeRoot != null)
		{
			treeRoot.ChildEnteredTree -= OnChildEnteredTreeRoot;
			treeRoot = null;
		}
	}

	private static readonly List<LifetimeScope> WaitingList = new(4);

	internal static bool WaitingListContains(LifetimeScope lifetimeScope)
		=> WaitingList.Contains(lifetimeScope);

	internal static void EnqueueReady(LifetimeScope lifetimeScope)
		=> WaitingList.Add(lifetimeScope);

	internal static void CancelReady(LifetimeScope lifetimeScope)
		=> WaitingList.Remove(lifetimeScope);

	public static void ReadyWaitingChildren(LifetimeScope awakenParent)
	{
		if (WaitingList.Count <= 0) return;

		List<LifetimeScope> buffer = new();
		for (int i = WaitingList.Count - 1; i >= 0; i--)
		{
			LifetimeScope waitingScope = WaitingList[i];
			waitingScope.SetParent(awakenParent);
			WaitingList.RemoveAt(i);
			buffer.Add(waitingScope);
		}

		foreach (LifetimeScope waitingScope in buffer)
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

		List<LifetimeScope> buffer = new();
		for (int i = WaitingList.Count - 1; i >= 0; i--)
		{
			LifetimeScope waitingScope = WaitingList[i];
			if (child.GetTree().CurrentScene != waitingScope.GetTree().CurrentScene)
				continue;

			WaitingList.RemoveAt(i);
			buffer.Add(waitingScope);
		}

		foreach (LifetimeScope waitingScope in buffer)
		{
			waitingScope._Ready(); // Re-throw if parent not found
		}
	}
}