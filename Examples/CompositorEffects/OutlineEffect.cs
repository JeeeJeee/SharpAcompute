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
    
    protected override void InitEffect()
    {
        _depthSampler = AcomputeShaderInstance.CreateSampler(RenderingDevice.SamplerFilter.Nearest,
            RenderingDevice.SamplerFilter.Nearest);
    }

    public override void AcomputeRenderCallback(int effectCallbackType, RenderData renderData,
        RenderSceneBuffersRD renderSceneBuffersRd)
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
        
        for (uint view = 0; view < renderSceneBuffersRd.GetViewCount(); view++)
        {
            Rid inputImage = renderSceneBuffersRd.GetColorLayer(view);
            Rid depthImage = renderSceneBuffersRd.GetDepthLayer(view);
            
            byte[] uniformByteArray = ToByteArray([OutlineThickness,DepthDistance,DepthBias,OnlyOutlines ? 1 : 0]);
            
            AcomputeShaderInstance.SetTexture(0, 0, inputImage);
            AcomputeShaderInstance.SetUniformBuffer(0, 1, uniformByteArray);
            
            AcomputeShaderInstance.SetTexture(1, 0, depthImage, RenderingDevice.UniformType.SamplerWithTexture, _depthSampler);
            AcomputeShaderInstance.SetPushConstant(ToByteArray(screenSizePushConstant));
            
            AcomputeShaderInstance.Dispatch(0, xGroup, yGroup, zGroup);
        }
    }
}
