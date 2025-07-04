﻿using Godot;
using SharpAcompute.CompositorEffect;

namespace SharpAcompute.Examples.CompositorEffects;

[GlobalClass]
[Tool]
public partial class DitherEffect : AcomputeCompositorEffect
{
    [Export(PropertyHint.Range, "0.0, 1.0")] public float DitherStrength = 0.5f;
    [Export(PropertyHint.Range, "1, 1024")] public int NumColors = 128;
    [Export(PropertyHint.Range, "1, 10")] public int DitherSize = 2;
    [Export] public bool UseGrayscaleDither;
    
    public override void AcomputeRenderCallback(int effectCallbackType, RenderData renderData,
        RenderSceneBuffersRD renderSceneBuffersRd)
    {
        uint xGroup = ((uint)SceneBuffersInternalSize.X - 1) / 8 + 1;
        uint yGroup = ((uint)SceneBuffersInternalSize.Y - 1) / 8 + 1;
        uint zGroup = 1;
        
        float[] screenSizePushConstant = new []{SceneBuffersInternalSize.X, SceneBuffersInternalSize.Y, 0.0f, 0.0f};
        for (uint view = 0; view < renderSceneBuffersRd.GetViewCount(); view++)
        {
            Rid inputImage = renderSceneBuffersRd.GetColorLayer(view);
            
            byte[] uniformByteArray = ToByteArray([DitherStrength,NumColors,DitherSize,UseGrayscaleDither ? 1 : 0]);
            
            AcomputeShaderInstance.SetTexture(0, 0, inputImage);
            AcomputeShaderInstance.SetUniformBuffer(0, 1, uniformByteArray);
            AcomputeShaderInstance.SetPushConstant(ToByteArray(screenSizePushConstant));
            
            AcomputeShaderInstance.Dispatch(0, xGroup, yGroup, zGroup);
        }
    }
}