namespace Manifesto.AspNet.Controllers;

using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ManifestV1ListController : ControllerBase
{
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1ListController(
        IKeyspaceSource keyspaceSource
    )
    {
        this.keyspaceSource = keyspaceSource;
    }

    [HttpGet(Name = "ListResourceManifests")]
    public async Task<IActionResult> ListManifestsAsync(
        [FromRoute] string group,
        [FromRoute] string version,
        [FromRoute] string kind,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest("Unknown manifest kind, group or version");
        }

        try
        {
            var response = await client.GetRangeAsync(keyspace, cancellationToken: cancellationToken);
            var manifests = response.Kvs
                .Select(x => JsonSerializer.Deserialize<Manifest>(x.Value.Span))
                .ToArray();
            return Ok(manifests);
        }
        catch (Exception _)
        {
            return this.Problem("failed to list resources.");
        }
    }
}