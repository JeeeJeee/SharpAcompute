using System.Linq;
using Godot;
using Godot.Collections;

namespace SharpAcompute.ShaderCompiler;

/// <summary>
/// Responsible for binding shader uniforms and actually dispatching a shader
/// </summary>
/// <param name="shaderResource"></param>
[Tool]
public partial class AcomputeShaderInstance() : RefCounted
{
    private RenderingDevice _rd = RenderingServer.GetRenderingDevice();
    private Array<Rid> _uniformSetRids = new();
    private readonly Rid[] _kernels;
    private Rid[] _pipelines;
    private byte[] _pushConstant;
    
    private System.Collections.Generic.Dictionary<(int set, int binding), RDUniform> _uniformSetCache = new();
    private System.Collections.Generic.Dictionary<(int set, int binding), byte[]> _uniformBufferCache = new();
    private System.Collections.Generic.Dictionary<(int set, int binding), Rid> _uniformBufferIdCache = new();

    private bool _refreshUniforms = true;

    public AcomputeShaderInstance(Rid[] shaderKernels) : this()
    {
	    _kernels = shaderKernels;
	    _rd = RenderingServer.GetRenderingDevice();
	    _pipelines = new Rid[shaderKernels.Length];
	    for(int i = 0; i < shaderKernels.Length; i++)
	    {
		    _pipelines[i] = _rd.ComputePipelineCreate(shaderKernels[i]);
	    }
	    AcomputeShaderCompiler.Instance.ShaderInstances.Add(this);
    }
    
    public void SetPushConstant(byte[] pushConstant)
    {
        _pushConstant = pushConstant;
    }
    
    public void SetTexture(int set, int binding, Rid texture,
	    RenderingDevice.UniformType uniformType = RenderingDevice.UniformType.Image, Rid sampler = default)
    {
        RDUniform rdUniform = new RDUniform();
        rdUniform.UniformType = uniformType;
        rdUniform.Binding = binding;
        if (sampler.IsValid)
        {
	        rdUniform.AddId(sampler);
        }
        rdUniform.AddId(texture);
        
        CacheUniform(rdUniform, set);
    }

    public void SetUniformBuffer(int set, int binding, byte[] uniformArray)
    {
	    var key = (set, binding);

	    if (_uniformBufferCache.ContainsKey(key))
	    {
		    _rd.BufferUpdate(_uniformBufferIdCache[key], 0, (uint)uniformArray.Length, uniformArray);
		    _uniformBufferCache[key] = uniformArray;
	    }
	    else
	    {
		    Rid uniformBufferId = _rd.UniformBufferCreate((uint)uniformArray.Length, uniformArray);
		    RDUniform u = new RDUniform
		    {
			    UniformType = RenderingDevice.UniformType.UniformBuffer,
			    Binding = binding
		    };
		    u.AddId(uniformBufferId);

		    _uniformBufferCache[key] = uniformArray;
		    AcomputeShaderCompiler.Instance.EffectRids.Add(uniformBufferId);
		    _uniformBufferIdCache[key] = uniformBufferId;

		    CacheUniform(u, set);
	    }
    }

    private void CacheUniform(RDUniform rdUniform, int set)
    {
	    var key = (set, rdUniform.Binding);

	    if (_uniformSetCache.TryGetValue(key, out RDUniform existingUniform))
	    {
		    Array<Rid> oldIds = existingUniform.GetIds();
		    Array<Rid> newIds = rdUniform.GetIds();

		    if (oldIds.Count != newIds.Count)
		    {
			    _refreshUniforms = true;
		    }
		    else
		    {
			    for (int i = 0; i < oldIds.Count; i++)
			    {
				    if (oldIds[i].Id != newIds[i].Id)
				    {
					    _refreshUniforms = true;
					    break;
				    }
			    }
		    }
	    }
	    else
	    {
		    _refreshUniforms = true;
	    }

	    _uniformSetCache[key] = rdUniform;
	    
	    if (_uniformSetRids.Count - 1 < set)
	    {
		    _uniformSetRids.Resize(set + 1);
	    }
    }
    
    public void Dispatch(int kernelIndex, uint xGroups, uint yGroups, uint zGroups)
    {
	    if (_kernels == null || !_kernels[kernelIndex].IsValid)
	    {
		    return;
	    }
	    
	    // Reallocate GPU memory if uniforms need updating
	    if (_refreshUniforms)
	    {
		    for (int i = 0; i < _uniformSetRids.Count; i++)
		    {
			    if(_uniformSetRids[i].IsValid) { _rd.FreeRid(_uniformSetRids[i]); }

			    RDUniform[] rdUniforms = _uniformSetCache
				    .Where(kvp => kvp.Key.set == i)
				    .Select(kvp => kvp.Value)
				    .ToArray();

			    Array<RDUniform> happyNow = new Array<RDUniform>(rdUniforms);
			    _uniformSetRids[i] = _rd.UniformSetCreate(happyNow, _kernels[0], (uint) i);
		    }
		    
		    _refreshUniforms = false;
	    }

	    long computeList = _rd.ComputeListBegin();
	    _rd.ComputeListBindComputePipeline(computeList, _pipelines[kernelIndex]);
	    for(int i = 0; i < _uniformSetRids.Count; i++)
	    {
		    _rd.ComputeListBindUniformSet(computeList, _uniformSetRids[i], (uint)i);   
	    }
	    _rd.ComputeListSetPushConstant(computeList, _pushConstant, (uint)_pushConstant.Length);
	    _rd.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
	    _rd.ComputeListEnd();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
	        foreach (Rid uniformSet in _uniformSetRids)
	        {
				if(uniformSet.IsValid) { _rd?.FreeRid(uniformSet); }    
	        }
	        
            foreach(Rid pipeline in _pipelines)
            {
	            _rd?.FreeRid(pipeline);
            }
            
            foreach (Rid binding in _uniformBufferIdCache.Values)
            {
	            _rd?.FreeRid(binding);
            }
        }
    }
}