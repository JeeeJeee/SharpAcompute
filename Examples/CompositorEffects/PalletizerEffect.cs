using Godot;
using SharpAcompute.CompositorEffect;

namespace SharpAcompute.Examples.CompositorEffects;

[GlobalClass]
[Tool]
public partial class PalletizerEffect : AcomputeCompositorEffect
{
    [Export(PropertyHint.Range, "0.0, 1.0")] public float EffectStrength = 0.5f;

    [Export]
    public CompressedTexture2D PaletteTexture
    {
        get => _paletteTexture;
        set
        {
            if (_paletteTexture != value)
            {
                _bRecreateTextureRid = true;
            }
            _paletteTexture = value;
        }
    }

    private bool _bRecreateTextureRid = true;
    private Rid _paletteTextureSampler;
    private Rid _paletteTextureRid;
    private CompressedTexture2D _paletteTexture;

    protected override void InitEffect()
    {
        _paletteTextureSampler = AcomputeShaderInstance.CreateSampler(RenderingDevice.SamplerFilter.Nearest, RenderingDevice.SamplerFilter.Nearest); 
    }
    
    public override void AcomputeRenderCallback(int effectCallbackType, RenderData renderData,
        RenderSceneBuffersRD renderSceneBuffersRd)
    {
        uint xGroup = ((uint)SceneBuffersInternalSize.X - 1) / 8 + 1;
        uint yGroup = ((uint)SceneBuffersInternalSize.Y - 1) / 8 + 1;
        uint zGroup = 1;

        if (PaletteTexture == null) { return; }
        if (!_paletteTextureRid.IsValid || _bRecreateTextureRid || !Rd.TextureIsValid(_paletteTextureRid))
        {
            _paletteTextureRid = AcomputeShaderInstance.CreateTexture(PaletteTexture.GetImage());
            _bRecreateTextureRid = false;
        }
        
        float[] screenSizePushConstant = [SceneBuffersInternalSize.X, SceneBuffersInternalSize.Y, 0.0f, 0.0f];
        for (uint view = 0; view < renderSceneBuffersRd.GetViewCount(); view++)
        {
            Rid inputImage = renderSceneBuffersRd.GetColorLayer(view);
            
            byte[] uniformByteArray = ToByteArray([EffectStrength,0,0,0]);
            
            AcomputeShaderInstance.SetTexture(0, 0, inputImage);
            AcomputeShaderInstance.SetTexture(0, 1, _paletteTextureRid, RenderingDevice.UniformType.SamplerWithTexture, _paletteTextureSampler);
            AcomputeShaderInstance.SetUniformBuffer(0, 2, uniformByteArray);
            AcomputeShaderInstance.SetPushConstant(ToByteArray(screenSizePushConstant));
            
            AcomputeShaderInstance.Dispatch(0, xGroup, yGroup, zGroup);
        }
    }
}
