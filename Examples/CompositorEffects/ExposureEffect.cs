using Godot;
using SharpAcompute.CompositorEffect;

namespace SharpAcompute.Examples.CompositorEffects;

[GlobalClass]
[Tool]
public partial class ExposureEffect : AcomputeCompositorEffect
{
    [Export] public Vector4 Exposure = new(2, 1, 1, 1);

    public override void AcomputeRenderCallback(int effectCallbackType, RenderData renderData,
        RenderSceneBuffersRD renderSceneBuffersRd)
    {
        uint xGroup = ((uint)SceneBuffersInternalSize.X - 1) / 8 + 1;
        uint yGroup = ((uint)SceneBuffersInternalSize.Y - 1) / 8 + 1;
        uint zGroup = 1;
        
        float[] screenSizePushConstant = [SceneBuffersInternalSize.X, SceneBuffersInternalSize.Y, 0.0f, 0.0f];
        for (uint view = 0; view < renderSceneBuffersRd.GetViewCount(); view++)
        {
            Rid inputImage = renderSceneBuffersRd.GetColorLayer(view);
            
            // Pack the exposure vector into a byte array
            byte[] uniformByteArray = ToByteArray([Exposure.X, Exposure.Y, Exposure.Z, Exposure.W]);
            
            // ACompute handles uniform caching under the hood, as long as the exposure value doesn't change or the render target doesn't change, these functions will only do work once
            AcomputeShaderInstance.SetTexture(0, 0, inputImage);
            AcomputeShaderInstance.SetUniformBuffer(0, 1, uniformByteArray);
            AcomputeShaderInstance.SetPushConstant(ToByteArray(screenSizePushConstant));
            
            // Dispatch the compute kernel
            AcomputeShaderInstance.Dispatch(0, xGroup, yGroup, zGroup);
        }
    }
}