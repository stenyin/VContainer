using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Godot;
using Environment = System.Environment;
using Thread = System.Threading.Thread;

// ReSharper disable once CheckNamespace
namespace RiderTestRunner;

// ReSharper disable once UnusedType.Global
public partial class NetCoreRunner : Node
{
	string runnerAssemblyPath;

	public override void _Ready()
	{
		var textNode = GetNode<RichTextLabel>("RichTextLabel");
		if (textNode == null)
			return;

		foreach (var arg in OS.GetCmdlineArgs())
		{
			textNode.Text += Environment.NewLine + arg;
		}

		if (OS.GetCmdlineArgs().Length < 4)
			return;

		var unitTestArgs = OS.GetCmdlineArgs()[4].Split([' '], StringSplitOptions.RemoveEmptyEntries).ToArray();
		runnerAssemblyPath = OS.GetCmdlineArgs()[2];

		var runnerLoadContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
		if (runnerLoadContext == null)
			return;

		runnerLoadContext.LoadFromAssemblyPath(runnerAssemblyPath);

		runnerLoadContext.Resolving += CurrentDomainOnAssemblyResolve;
		AssemblyLoadContext.Default.Resolving += CurrentDomainOnAssemblyResolve;

		var thread = new Thread(() =>
		{
			AppDomain.CurrentDomain.ExecuteAssembly(runnerAssemblyPath, unitTestArgs);
			GetTree().Quit();
		});

		thread.Start();
	}

	Assembly CurrentDomainOnAssemblyResolve(AssemblyLoadContext loadContext, AssemblyName assemblyName)
	{
		// not sure, if this is needed
		var alreadyLoadedMatch = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(loadedAssembly =>
		{
			var name = loadedAssembly.GetName().Name;
			return name != null &&
			       name.Equals(assemblyName.Name);
		});

		if (alreadyLoadedMatch != null)
		{
			return alreadyLoadedMatch;
		}

		var dir = new FileInfo(runnerAssemblyPath).Directory;
		if (dir == null) return null;
		var file = new FileInfo(Path.Combine(dir.FullName, $"{assemblyName.Name}.dll"));
		if (file.Exists)
			return loadContext.LoadFromAssemblyPath(file.FullName);

		return null;
	}
}