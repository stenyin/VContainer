using System;
using VContainer.Internal;

namespace VContainer.Godot;

public readonly struct EntryPointsBuilder
{
	public static void EnsureDispatcherRegistered(IContainerBuilder containerBuilder)
	{
		if (containerBuilder.Exists(typeof(EntryPointDispatcher), false)) return;
		containerBuilder.Register<EntryPointDispatcher>(Lifetime.Scoped);
		containerBuilder.RegisterBuildCallback(container => { container.Resolve<EntryPointDispatcher>().Dispatch(); });
	}

	readonly IContainerBuilder containerBuilder;
	readonly Lifetime lifetime;

	public EntryPointsBuilder(IContainerBuilder containerBuilder, Lifetime lifetime)
	{
		this.containerBuilder = containerBuilder;
		this.lifetime = lifetime;
	}

	public RegistrationBuilder Add<T>() => containerBuilder.Register<T>(lifetime).AsImplementedInterfaces();

	public void OnException(Action<Exception> exceptionHandler) => containerBuilder.RegisterEntryPointExceptionHandler(exceptionHandler);
}
