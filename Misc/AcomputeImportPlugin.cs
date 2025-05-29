#if TOOLS
using Godot;
using Godot.Collections;
using SharpAcompute.Resources;

namespace SharpAcompute.Misc;

/// <summary>
/// Handles importing of acompute files which are internally saved as .tres files.
/// Note that all these functions need to be implemented even if they do nothing otherwise Godot will spit out errors
/// </summary>
[GlobalClass]
public partial class AcomputeImportPlugin : EditorImportPlugin
{
    public override string _GetImporterName()
    {
        return "SharpAcompute.plugin";
    }

    public override string _GetVisibleName()
    {
        return "Acompute Shader Resource";
    }

    public override string[] _GetRecognizedExtensions()
    {
        return ["acompute"];
    }

    public override string _GetSaveExtension()
    {
        return "tres";
    }

    public override string _GetResourceType()
    {
        return "AcomputeShaderResource";
    }

    public override int _GetPresetCount()
    {
        return 1;
    }

    public override string _GetPresetName(int presetIndex)
    {
        return "Default";
    }

    public override float _GetPriority()
    {
        return 2.0f;
    }

    public override int _GetImportOrder()
    {
        return base._GetImportOrder();
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetImportOptions(string path, int presetIndex)
    {
        return
        [
            new Godot.Collections.Dictionary
            {
                { "name", "myOption" },
                { "default_value", false },
            },
        ];
    }

    public override bool _GetOptionVisibility(string path, StringName optionName, Dictionary options)
    {
        return base._GetOptionVisibility(path, optionName, options);
    }

    public override Error _Import(string sourceFile, string savePath, Godot.Collections.Dictionary options, Godot.Collections.Array<string> platformVariants, Godot.Collections.Array<string> genFiles)
    {
        using var file = FileAccess.Open(sourceFile, FileAccess.ModeFlags.Read);
        if (file.GetError() != Error.Ok)
        {
            return Error.Failed;
        }

        string sourceCode = file.GetAsText();
        
        AcomputeShaderResource shaderResource = new();
        shaderResource.SourceCode = sourceCode;
        
        string filename = $"{savePath}.{_GetSaveExtension()}";
        Error saved = ResourceSaver.Save(shaderResource, filename);
        return saved;
    }
}
#endif