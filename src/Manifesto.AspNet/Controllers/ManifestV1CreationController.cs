namespace Manifesto.AspNet.Controllers;

using System.Text.Json;
using System.Text.Json.Serialization;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ManifestV1CreationController : ControllerBase
{
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1CreationController(
        IKeyspaceSource keyspaceSource
    )
    {
        this.keyspaceSource = keyspaceSource;
    }

    [HttpPut(Name = "PutResourceManifest")]
    public async Task<IActionResult> PutManifestAsync(
        string group,
        string version,
        string kind,
        Manifest manifest,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest("Unknown manifest kind, group or version");
        }

        var key = Path.Combine(keyspace, manifest.Metadata.Name);

        var typeSanitizedManifest = manifest with { Group = group, Kind = kind, Version = version };
        var serializedManifest = JsonSerializer.Serialize(typeSanitizedManifest);

        try
        {
            await client.PutAsync(key, serializedManifest, cancellationToken: cancellationToken);
        }
        catch (Exception _)
        {
            return this.Problem("failed to create or update resource from the given manifest");
        }

        return Ok();
    }
}