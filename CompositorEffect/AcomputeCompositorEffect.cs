using System.Threading.Tasks;
using Godot;
using SharpAcompute.ShaderCompiler;
using SharpAcompute.Resources;

namespace SharpAcompute.CompositorEffect;

[GlobalClass][Tool]
public partial class AcomputeCompositorEffect : Godot.CompositorEffect
{
    protected RenderingDevice Rd = RenderingServer.GetRenderingDevice();

    // Parameterless constructor is needed for hot-reload, otherwise we get a crash
    public AcomputeCompositorEffect() {}

    protected AcomputeShaderInstance AcomputeShaderInstance
    {
        get => _acomputeShaderInstance;
        set
        {
            if (value == _acomputeShaderInstance)
            {
                return;
            }
            
            if (value == null)
            {
                _acomputeShaderInstance?.Free();
                _acomputeShaderInstance = null;
                return;
            }

            if (value != _acomputeShaderInstance)
            {
                _acomputeShaderInstance?.Free();
            }
            
            _acomputeShaderInstance = value;
        }
    }
    private AcomputeShaderInstance _acomputeShaderInstance;

    [Export]
    public AcomputeShaderResource AcomputeShaderResource
    {
        get => _acomputeShaderResource;
        set
        {
            if (value == _acomputeShaderResource)
            {
                return;
            }
            
            if (value == null)
            {
                // Free the shader because we aren't using it anymore
                if(AcomputeShaderResource != null) {AcomputeShaderResource.ResourceChanged -= OnShaderResourceModified;}
                _acomputeShaderResource = null;
                AcomputeShaderInstance = null;
                return;
            }
            
            // Unsubscribe from old shader resource and subscribe to new shader resource
            if(AcomputeShaderResource != null) { AcomputeShaderResource.ResourceChanged -= OnShaderResourceModified; }
            _acomputeShaderResource = value;
            _acomputeShaderResource.ResourceChanged += OnShaderResourceModified;
            
            // When hot-reloading C# tool scripts godot calls all property setters. It does this in a random order so we use CallDeferred to make sure other files that were hot-reloaded have actually done so
            CallDeferred(nameof(HotReloadEffect));
        }
    }
    private AcomputeShaderResource _acomputeShaderResource;
    
    protected Vector2I SceneBuffersInternalSize;
    protected RenderSceneBuffersRD RenderSceneBuffersRd;
    
    public async void HotReloadEffect()
    {
        if (AcomputeShaderResource != null)
        {
            await InitializeCompositorEffect(_acomputeShaderResource);   
        }
    }
    
    private Task InitializeCompositorEffect(AcomputeShaderResource shaderResource)
    {
        // In a deployed game the compositor effects may be ready before the autload is. In that case we wait here
        while (AcomputeShaderCompiler.Instance == null)
        {
            Task.Delay(500);
        }
    
        if (AcomputeShaderCompiler.Instance.GetComputeKernelCompilations(shaderResource, out Rid[] kernelCompilations))
        {
            AcomputeShaderInstance = new AcomputeShaderInstance(kernelCompilations);
            InitEffect();
        }
        else
        {
            AcomputeShaderInstance = null;
        }
    
        return Task.CompletedTask;
    }

    /// <summary>
    /// This function acts as the constructor for anything to do with RIDs
    /// Keep in mind that this function will be called in the editor since compositor effects are Tool scripts.
    /// So more complicated interactions with the scene tree might fail in the editor
    /// </summary>
    protected virtual void InitEffect() { }
    
    /// <summary>
    /// Override this
    /// </summary>
    /// <param name="effectCallbackType"></param>
    /// <param name="renderData"></param>
    public virtual void AcomputeRenderCallback(int effectCallbackType, RenderData renderData){}

    /// <summary>
    /// Called when the shader resource has been re/saved in the editor.
    /// We have to throw the shader instance out and make a new one
    /// </summary>
    private void OnShaderResourceModified()
    {
        AcomputeShaderCompiler acomputeShaderCompiler = AcomputeShaderCompiler.Instance;
        
        // Update the entry then create a new ShaderInstance
        if (acomputeShaderCompiler.UpdateShaderEntry(AcomputeShaderResource, out Rid[] compiledKernels))
        {
            AcomputeShaderInstance = new AcomputeShaderInstance(compiledKernels);
            return;
        }

        AcomputeShaderInstance = null;
    }
    
    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        if (!Enabled || AcomputeShaderResource == null || AcomputeShaderInstance == null)
        {
            return;
        }

        if (Rd == null)
        {
            GD.PrintErr("Rendering device is null!");
            return;
        }
        
        RenderSceneBuffersRd = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        if (RenderSceneBuffersRd == null)
        {
            GD.PrintErr("No render scene buffers found!");
            return;
        }
        
        SceneBuffersInternalSize = RenderSceneBuffersRd.GetInternalSize();
        if (SceneBuffersInternalSize.X == 0 || SceneBuffersInternalSize.Y == 0)
        {
            GD.PrintErr("Rendering to 0x0 buffer!");
            return;
        }
        
        AcomputeRenderCallback(effectCallbackType, renderData);
    }
    
    public static byte[] ToByteArray(float[] floatArray)
    {
        byte[] uniformByteArray = new byte[floatArray.Length * sizeof(float)];
        System.Buffer.BlockCopy(floatArray, 0, uniformByteArray, 0, floatArray.Length * sizeof(float));
        return uniformByteArray;
    }
}