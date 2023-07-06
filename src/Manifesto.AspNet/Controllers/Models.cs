namespace Manifesto.AspNet.Controllers;

public record struct Metadata
{
    public string Name { get; init; }
    public IDictionary<string, string> Labels { get; init; }
    public IDictionary<string, string> Annotations { get; init; }
    public string? Revision { get; init; }
}

public record struct Manifest
{
    public string? Kind { get; init; }
    public string? Group { get; init; }
    public string? Version { get; init; }
    public Metadata Metadata { get; init; }
    public dynamic Spec { get; init; }
}