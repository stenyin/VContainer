using System;

namespace VContainer.Godot;

public sealed class EntryPointExceptionHandler(Action<Exception> handler)
{
	public void Publish(Exception ex)
	{
		handler.Invoke(ex);
	}
}
