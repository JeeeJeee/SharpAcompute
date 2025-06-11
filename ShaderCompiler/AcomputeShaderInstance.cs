using System.Linq;
using Godot;
using Godot.Collections;

namespace SharpAcompute.ShaderCompiler;

/// <summary>
/// Responsible for binding shader uniforms and actually dispatching a shader
/// </summary>
/// <param name="shaderResource"></param>
[Tool]
public partial class AcomputeShaderInstance : GodotObject, ISerializationListener
{
    private RenderingDevice _rd = RenderingServer.GetRenderingDevice();
    private Array<Rid> _uniformSetRids = new();
    private readonly Rid[] _kernels;
    private Rid[] _pipelines;
    private byte[] _pushConstant;
    private Array<Rid> _textureSamplers = new();
    private Array<Rid> _textures = new();
    
    private System.Collections.Generic.Dictionary<(int set, int binding), RDUniform> _uniformSetCache = new();
    private System.Collections.Generic.Dictionary<(int set, int binding), byte[]> _uniformBufferCache = new();
    private System.Collections.Generic.Dictionary<(int set, int binding), Rid> _uniformBufferIdCache = new();

    private bool _refreshUniforms = true;
    
    // Parameterless constructor is needed for hot-reload, otherwise we get a crash
    private AcomputeShaderInstance() { }
    
    public AcomputeShaderInstance(Rid[] shaderKernels)
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

    /// <summary>
    /// Automatically frees created samplers
    /// </summary>
    /// <param name="MinFilter"></param>
    /// <param name="MagFilter"></param>
    /// <returns>A texture sampler RID</returns>
    public Rid CreateSampler(RenderingDevice.SamplerFilter MinFilter, RenderingDevice.SamplerFilter MagFilter)
    {
	    RDSamplerState samplerState = new RDSamplerState();
	    samplerState.MinFilter = MinFilter;
	    samplerState.MagFilter = MagFilter;
	    Rid textureSampler = _rd.SamplerCreate(samplerState);
	    _textureSamplers.Add(textureSampler);
	    return textureSampler;
    }
    
    public void SetPushConstant(byte[] pushConstant)
    {
        _pushConstant = pushConstant;
    }

    /// <summary>
    /// Automatically frees created textures
    /// </summary>
    /// <param name="inputImage"></param>
    /// <returns>A texture RID</returns>
    public Rid CreateTexture(Image inputImage, RenderingDevice.DataFormat format = RenderingDevice.DataFormat.R8G8B8A8Srgb, RenderingDevice.TextureUsageBits usageBits = RenderingDevice.TextureUsageBits.SamplingBit)
    {
	    inputImage.ClearMipmaps();
	    inputImage.Decompress();
	    inputImage.Convert(Image.Format.Rgba8);
            
	    RDTextureFormat textureFormat = new RDTextureFormat();
	    textureFormat.Width = (uint)inputImage.GetWidth();
	    textureFormat.Height = (uint)inputImage.GetHeight();
	    textureFormat.Format = format;
	    textureFormat.UsageBits = usageBits;
	    
	    Rid textureRid = _rd.TextureCreate(textureFormat, new RDTextureView(), new Array<byte[]>{inputImage.GetData()});
	    _textures.Add(textureRid);
	    
	    return textureRid;
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

	    // @TODO: Bandaid fix: Sometimes the _uniformBufferCache has a key but the bufferIDCache won't have the key. Not sure how so we do two checks
	    if (_uniformBufferCache.ContainsKey(key) && _uniformBufferIdCache.ContainsKey(key))
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
			    if(_uniformSetRids[i].IsValid && _rd.UniformSetIsValid(_uniformSetRids[i]))
			    {
				    _rd.FreeRid(_uniformSetRids[i]);
				    _uniformSetRids[i] = default;
			    }

			    RDUniform[] rdUniforms = _uniformSetCache
				    .Where(kvp => kvp.Key.set == i)
				    .Select(kvp => kvp.Value)
				    .ToArray();
			    
			    _uniformSetRids[i] = _rd.UniformSetCreate(new Array<RDUniform>(rdUniforms), _kernels[0], (uint) i);
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
	        FreeRids();
        }
    }

    private void FreeRids()
    {
		// Apparently clearing RIDs that a uniformSet rid references automatically clears the uniformSet using them as well.
		// So we don't need to free the uniformSetRids here!? https://github.com/godotengine/godot/issues/103073
		// foreach (Rid uniformSet in _uniformSetRids)
		// {
		// 	if(uniformSet.IsValid) { _rd?.FreeRid(uniformSet); }    
		// }

		if (_rd == null)
		{
			GD.PrintErr("AcomputeShaderInstance: Failed to free RIDs! RenderingDevice was null! In: " + this);
		}

		if (_pipelines != null)
		{
			foreach(Rid pipeline in _pipelines)
			{
				if (_rd!.ComputePipelineIsValid(pipeline))
				{
					_rd.FreeRid(pipeline);	
				}
			}	
		}
            
	    foreach (Rid binding in _uniformBufferIdCache.Values)
	    {
		    _rd!.FreeRid(binding);
	    }
	    
	    foreach (var sampler in _textureSamplers)
	    {
		    _rd!.FreeRid(sampler);
	    }

	    foreach (var texture in _textures)
	    {
		    if (_rd!.TextureIsValid(texture))
		    {
			    _rd.FreeRid(texture);   
		    }
	    }
	    

	    _textureSamplers.Clear();
	    _textures.Clear();
    }

    public void OnBeforeSerialize()
    { 
	    FreeRids();
    }
    public void OnAfterDeserialize() { }
}