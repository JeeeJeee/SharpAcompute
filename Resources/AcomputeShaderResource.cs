using Godot;

namespace SharpAcompute.Resources;

public delegate void AcomputeShaderResourceChanged();

[GlobalClass]
[Tool]
public partial class AcomputeShaderResource : Resource
{
    [Export(PropertyHint.MultilineText)]
    public string SourceCode {get; set;}
    
    public AcomputeShaderResourceChanged ResourceChanged;

    public void OnChanged()
    {
        // We are not using EmitChanged() because godot calls that in weird places causing erroneous shader recompiles
        ResourceChanged?.Invoke();
    }
}