#if TOOLS
using SharpAcompute.Resources;
using Godot;
using SharpAcompute.Misc;

namespace SharpAcompute;

[Tool]
public partial class SharpAcompute : EditorPlugin
{
	private AcomputeImportPlugin _importPlugin = new();

	public override void _EnablePlugin()
	{
		AddAutoloadSingleton("AcomputeShaderCompiler", "res://addons/SharpAcompute/ShaderCompiler/AcomputeShaderCompiler.cs");
		ProjectSettings.Save();
	}
	
	public override void _DisablePlugin()
	{
		RemoveAutoloadSingleton("AcomputeShaderCompiler");
		ProjectSettings.Save();
	}

	public override void _EnterTree()
	{
		// Shaders need to be recompiled when a shader resource is changed
		// ResourceSaved is only ever called for resources modified inside the editor.
		// Resources that are imported need to be handled with ResourcesReimported
		ResourceSaved += resource =>
		{
			if (resource is AcomputeShaderResource shaderResource)
			{
				shaderResource.OnChanged();
			}
		};
		EditorInterface.Singleton.GetResourceFilesystem().ResourcesReimported += resources =>
		{
			foreach (var resource in resources)
			{
				if(!resource.EndsWith(".acompute"))
				{
					continue;
				}
				
				GD.Load<AcomputeShaderResource>(resource).OnChanged();
			}
		};
		
		AddImportPlugin(_importPlugin);
	}

	public override void _ExitTree()
	{
		RemoveImportPlugin(_importPlugin);
	}

	public override string _GetPluginName()
	{
		return "SharpAcompute";
	}
}
#endif