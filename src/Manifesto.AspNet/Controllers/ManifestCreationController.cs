namespace Manifesto.AspNet.Controllers;

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
        CancellationToken cancellationToken)
    {
        return Ok(keyspaceSource.GetKeySpace(kind, version, group));
    }
}