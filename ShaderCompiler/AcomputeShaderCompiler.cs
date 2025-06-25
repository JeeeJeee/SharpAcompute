using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpAcompute.Resources;
using Godot;

namespace SharpAcompute.ShaderCompiler;

[Tool]
public partial class AcomputeShaderCompiler : Node, ISerializationListener
{
    private RenderingDevice _rd;
    private Dictionary<AcomputeShaderResource, Rid[]> _computeShaderKernelCompilations = new();
    
    public List<AcomputeShaderInstance> ShaderInstances = new();
    
    public static AcomputeShaderCompiler Instance { get; private set; }
    
    public override void _Ready()
    {
        GD.Print("SharpAcompute: Ready.");
        InitShaderCompiler();
    }

    // Called on hotreload
    private AcomputeShaderCompiler()
    {
        GD.Print("SharpAcompute: Hotreload Triggered");
        InternalFree();
        InitShaderCompiler();
    }

    private void InitShaderCompiler()
    {
        _rd = RenderingServer.GetRenderingDevice();
        Instance = this;
    }
    
    /// <summary>
    /// Returns an array containing the compiled shader Rid's
    /// </summary>
    /// <param name="computeShaderResource"></param>
    /// <returns></returns>
    public Rid[] CompileComputeShader(AcomputeShaderResource computeShaderResource)
    {
        Rid[] compileError(string reason)
        {
            GD.PrintErr("Failed to compile shader: " + computeShaderResource.ResourcePath);
            GD.PrintErr("Reason: " + reason);
            return [];
        }
        
        string rawShaderCodeString = computeShaderResource.SourceCode;

        Span<string> lines = rawShaderCodeString.Split('\n');
        var kernelNames = new List<string>();
        int lineIndex = 0;

        // Extract kernel names
        for (; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.StartsWith("#kernel "))
            {
                string kernelName = line.Substring("#kernel ".Length).Trim();
                kernelNames.Add(kernelName);
            }
            else
            {
                break;
            }
        }

        // Check for no kernels
        if (kernelNames.Count == 0)
        {
            return compileError("No kernels found");
        }

        // Check if remaining code exists
        if (lineIndex >= lines.Length)
        {
            return compileError("No shader code found");
        }

        // Join remaining lines for search
        StringBuilder remainingCodeBuilder = new StringBuilder();
        for (int i = lineIndex; i < lines.Length; i++)
        {
            remainingCodeBuilder.AppendLine(lines[i]);
        }

        // The code with the kernel defines at the top of the file removed
        string kernelDefineStrippedCode = remainingCodeBuilder.ToString();

        foreach (var kernelName in kernelNames)
        {
            if (!kernelDefineStrippedCode.Contains(kernelName))
            {
                return compileError($"{kernelName} kernel not found!");
            }
        }
        
        // Get kernel thread groups
        List<int> linesToRemove = new();
        var kernelToThreadGroupCount = new Dictionary<string, int[]>();
        for (int i = lineIndex; i < lines.Length; i++)
        {
            string line = lines[i];
            foreach (string kernelName in kernelNames)
            {
                // if (line.Contains(kernelName) && line.Contains("void"))
                if(line.StartsWith("void " + kernelName + "()"))
                {
                    if (i == 0)
                    {
                        return compileError("Kernel thread group count not found");
                    }

                    string prevLine = lines[i - 1].Trim();
                    if (prevLine.Contains("numthreads"))
                    {
                        try
                        {
                            int start = prevLine.IndexOf('(') + 1;
                            int end = prevLine.IndexOf(')');
                            int[] threadGroups = prevLine[start..end].Split(',').Select(x => int.Parse(x.Trim())).ToArray();

                            if (threadGroups.Length != 3)
                            {
                                return compileError("Kernel thread group syntax error");
                            }

                            kernelToThreadGroupCount[kernelName] = threadGroups;
                            linesToRemove.Add(i - 1);
                        }
                        catch
                        {
                            return compileError("Kernel thread group parse error");
                        }
                    }
                    else
                    {
                        return compileError("Kernel thread group count not found");
                    }
                }
            }
        }
        
        // Compiled Kernels
        List<Rid> compiledKernels = new(); 
        foreach (string kernelName in kernelNames)
        {
            StringBuilder shaderCodeBuilder = new StringBuilder("#version 450 \n");

            if (!kernelToThreadGroupCount.TryGetValue(kernelName, out var threadGroup) || threadGroup.Length != 3)
            {
                return compileError($"Thread group info missing or invalid for kernel: {kernelName}");
            }

            shaderCodeBuilder.AppendLine($"layout(local_size_x = {threadGroup[0]}, local_size_y = {threadGroup[1]}, local_size_z = {threadGroup[2]}) in;");

            // strip threadgroup lines in original code string
            for (int i = lineIndex; i < lines.Length; i++)
            {
                if (linesToRemove.Contains(i))
                {
                    shaderCodeBuilder.AppendLine("\n");
                    continue;
                }

                string line = lines[i];
                if (!line.StartsWith("void " + kernelName + "()"))
                {
                    shaderCodeBuilder.AppendLine(line);
                    continue;
                }
                
                shaderCodeBuilder.AppendLine(lines[i].Replace(kernelName, "main"));
            }

            string shaderCodeString = shaderCodeBuilder.ToString();
            
            RDShaderSource shaderSource = new RDShaderSource
            {
                Language = RenderingDevice.ShaderLanguage.Glsl,
                SourceCompute = shaderCodeString
            };

            RDShaderSpirV shaderSpirv = _rd.ShaderCompileSpirVFromSource(shaderSource);

            if (!string.IsNullOrEmpty(shaderSpirv.CompileErrorCompute))
            {
                GD.PrintErr(shaderSpirv.CompileErrorCompute);
                GD.PrintErr("In: " + shaderCodeString);
                return [];
            }

            Rid shaderHandle = _rd.ShaderCreateFromSpirV(shaderSpirv);
            if (!shaderHandle.IsValid)
            {
                return compileError("Failed to create shader from SpirV.");
            }
            
            compiledKernels.Add(shaderHandle);
        }
        
        GD.Print($"Acompute shader: {computeShaderResource.GetPath()} : Compiled successfully!");
        
        return compiledKernels.ToArray();
    }
    
    private string GetShaderName(AcomputeShaderResource shaderResource)
    {
        return shaderResource.GetName();
    }
    
    public bool GetComputeKernelCompilations(AcomputeShaderResource shaderResource, out Rid[] outkernelCompilations)
    {
        outkernelCompilations = [];
        
        // If the shader isn't in the dict we just compile it here
        if (!_computeShaderKernelCompilations.ContainsKey(shaderResource))
        {
            Rid[] compiledShaders = CompileComputeShader(shaderResource);
            if(compiledShaders == null || compiledShaders.Length == 0) { return false; }
            _computeShaderKernelCompilations[shaderResource] = compiledShaders;
        }

        outkernelCompilations = _computeShaderKernelCompilations[shaderResource];
        return true;
    }

    public void RemoveShaderEntry(AcomputeShaderResource shaderResource)
    {
        if (_computeShaderKernelCompilations.TryGetValue(shaderResource, out Rid[] kernelCompilations))
        {
            for (var index = 0; index < kernelCompilations.Length; index++)
            {
                if (kernelCompilations[index].IsValid)
                {
                    _rd.FreeRid(kernelCompilations[index]);
                    kernelCompilations[index] = default;
                }
            }

            _computeShaderKernelCompilations.Remove(shaderResource);
        }
    }

    /// <summary>
    /// Updates cached entry with recompiled kernels from shaderResource
    /// </summary>
    /// <param name="shaderResource"></param>
    /// <param name="outKernelCompilations"></param>
    /// <returns>Returns false if update failed</returns>
    public bool UpdateShaderEntry(AcomputeShaderResource shaderResource, out Rid[] outKernelCompilations)
    {
        outKernelCompilations = [];
        RemoveShaderEntry(shaderResource);
        
        Rid[] compiledShaders = CompileComputeShader(shaderResource);
        if(compiledShaders == null || compiledShaders.Length == 0) { return false; }
        
        _computeShaderKernelCompilations[shaderResource] = compiledShaders;
        outKernelCompilations = compiledShaders;
        return true;
    }
    
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            InternalFree();
        }
    }

    private void InternalFree()
    {
        foreach (var computeShader in _computeShaderKernelCompilations.Values)
        {
            for (var index = 0; index < computeShader.Length; index++)
            {
                var kernel = computeShader[index];
                if (kernel.IsValid)
                {
                    _rd.FreeRid(kernel);
                    kernel = default;
                }
            }
        }
        
        ShaderInstances?.ForEach(x => x?.Free());
        
        ShaderInstances?.Clear();
        _computeShaderKernelCompilations.Clear();
        
        GD.Print("SharpAcompute: Compute Shaders freed.");
    }
    
    public void OnBeforeSerialize()
    {
        // Makes sure that we clear the RIDs BEFORE we are reloading, otherwise they leak
        // Can't call the InternalFree method from here because some of the frees are handled by their own objects when reloaded and it would cause errors.
        foreach (var computeShader in _computeShaderKernelCompilations.Values)
        {
            for (var index = 0; index < computeShader.Length; index++)
            {
                var kernel = computeShader[index];
                if (kernel.IsValid)
                {
                    _rd.FreeRid(kernel);
                    kernel = default;
                }
            }
        }
    }
    public void OnAfterDeserialize() {}
}