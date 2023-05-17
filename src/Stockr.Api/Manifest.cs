namespace Stockr.Api;

public class Metadata {
    public string Name { get; set; }
    public IDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    public IDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();

}

public class Manifest
{
    public string? ApiVerion { get; set; }
    public string? ApiGroup { get; set; }
    public string? Kind { get; set; }
    public Metadata? Metadata { get; set; }
    public dynamic? Spec { get; set; }
}
