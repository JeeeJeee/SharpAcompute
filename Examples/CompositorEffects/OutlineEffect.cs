using Godot;
using SharpAcompute.CompositorEffect;
using SharpAcompute.ShaderCompiler;

namespace SharpAcompute.Examples.CompositorEffects;

[GlobalClass]
[Tool]
public partial class OutlineEffect : AcomputeCompositorEffect
{
    [ExportCategory("Outline Effect Parameters")]
    [Export(PropertyHint.Range,"1, 10")] public int OutlineThickness = 1;
    [Export] public float DepthDistance = 10.0f;
    [Export(PropertyHint.Range, "0.0, 1.0")] public float DepthBias = 0.1f;
    [Export] public bool OnlyOutlines;
    
    private Rid _depthSampler;
    
    /// <summary>
    /// Any new RIDs that the effect creates must be tracked by the ShaderCompiler.
    /// The shader compiler is guaranteed to be ready when this function is called 
    /// </summary>
    protected override void InitEffect()
    {
        RDSamplerState samplerState = new RDSamplerState();
        samplerState.MinFilter = RenderingDevice.SamplerFilter.Linear;
        samplerState.MagFilter = RenderingDevice.SamplerFilter.Linear;
        _depthSampler = Rd.SamplerCreate(samplerState);
        
        // Add the sampler to the list of resources that need to be cleared on exit
        AcomputeShaderCompiler.Instance.EffectRids.Add(_depthSampler);
    }

    public override void AcomputeRenderCallback(int effectCallbackType, RenderData renderData)
    {
        uint xGroup = ((uint)SceneBuffersInternalSize.X - 1) / 8 + 1;
        uint yGroup = ((uint)SceneBuffersInternalSize.Y - 1) / 8 + 1;
        uint zGroup = 1;

        Projection invProjection = renderData.GetRenderSceneData().GetCamProjection().Inverse();
        
        float[] screenSizePushConstant = [
            SceneBuffersInternalSize.X, 
            SceneBuffersInternalSize.Y,
            invProjection[2].W,
            invProjection[3].W];
        
        for (uint view = 0; view < RenderSceneBuffersRd.GetViewCount(); view++)
        {
            Rid inputImage = RenderSceneBuffersRd.GetColorLayer(view);
            Rid depthImage = RenderSceneBuffersRd.GetDepthLayer(view);
            
            byte[] uniformByteArray = ToByteArray([OutlineThickness,DepthDistance,DepthBias,OnlyOutlines ? 1 : 0]);
            
            AcomputeShaderInstance.SetTexture(0, 0, inputImage);
            AcomputeShaderInstance.SetUniformBuffer(0, 1, uniformByteArray);
            
            AcomputeShaderInstance.SetTexture(1, 0, depthImage, RenderingDevice.UniformType.SamplerWithTexture, _depthSampler);
            AcomputeShaderInstance.SetPushConstant(ToByteArray(screenSizePushConstant));
            
            AcomputeShaderInstance.Dispatch(0, xGroup, yGroup, zGroup);
        }
    }
}
