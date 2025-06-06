using System.Threading.Tasks;
using Godot;
using SharpAcompute.ShaderCompiler;
using SharpAcompute.Resources;

namespace SharpAcompute.CompositorEffect;

[GlobalClass][Tool]
public partial class AcomputeCompositorEffect : Godot.CompositorEffect
{
    protected RenderingDevice Rd = RenderingServer.GetRenderingDevice();
    
    protected AcomputeShaderInstance AcomputeShaderInstance { get; set; }

    [Export]
    public AcomputeShaderResource AcomputeShaderResource
    {
        get => _acomputeShaderResource;
        set
        {
            if(value == _acomputeShaderResource) { return; }
            
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
            
            _ = InitializeCompositorEffect(_acomputeShaderResource);
        }
    }
    private AcomputeShaderResource _acomputeShaderResource;
    
    protected Vector2I SceneBuffersInternalSize;
    protected RenderSceneBuffersRD RenderSceneBuffersRd;

    private async Task InitializeCompositorEffect(AcomputeShaderResource shaderResource)
    {
        while (AcomputeShaderCompiler.Instance == null)
        {
            await Task.Delay(100);
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
    }

    /// <summary>
    /// Override this function and do your setup there
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
            GD.PrintErr("Rendering device is null");
            return;
        }

        RenderSceneBuffersRd = renderData.GetRenderSceneBuffers() as RenderSceneBuffersRD;
        if (RenderSceneBuffersRd == null)
        {
            GD.PrintErr("No render scene buffers found");
            return;
        }

        SceneBuffersInternalSize = RenderSceneBuffersRd.GetInternalSize();
        if (SceneBuffersInternalSize.X == 0 || SceneBuffersInternalSize.Y == 0)
        {
            GD.PrintErr("Rendering to 0x0 buffer");
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