using Godot;
using VContainer;
using VContainer.Godot;

[GlobalClass]
public partial class TestLifetimeScope : LifetimeScope
{
	protected override void Configure(IContainerBuilder builder)
	{
		base.Configure(builder);
	}
}