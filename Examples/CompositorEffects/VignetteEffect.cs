using Godot;
using SharpAcompute.CompositorEffect;

namespace SharpAcompute.Examples.CompositorEffects;

[GlobalClass]
[Tool]
public partial class VignetteEffect : AcomputeCompositorEffect
{
    [Export(PropertyHint.Range, "0.0, 1.0")] public float VignetteStrength = 0.5f;

    public override void AcomputeRenderCallback(int effectCallbackType, RenderData renderData)
    {
        uint xGroup = ((uint)SceneBuffersInternalSize.X - 1) / 8 + 1;
        uint yGroup = ((uint)SceneBuffersInternalSize.Y - 1) / 8 + 1;
        uint zGroup = 1;
        
        float[] screenSizePushConstant = new []{SceneBuffersInternalSize.X, SceneBuffersInternalSize.Y, 0.0f, 0.0f};
        for (uint view = 0; view < RenderSceneBuffersRd.GetViewCount(); view++)
        {
            Rid inputImage = RenderSceneBuffersRd.GetColorLayer(view);
            
            byte[] uniformByteArray = ToByteArray([VignetteStrength,0,0,0]);
            
            AcomputeShaderInstance.SetTexture(0, 0, inputImage);
            AcomputeShaderInstance.SetUniformBuffer(0, 1, uniformByteArray);
            AcomputeShaderInstance.SetPushConstant(ToByteArray(screenSizePushConstant));
            
            AcomputeShaderInstance.Dispatch(0, xGroup, yGroup, zGroup);
        }
    }
}
