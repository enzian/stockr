using System.Text.Json;
using dotnet_etcd.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Stockr.Api.Controllers;

record ManifestRevision(string group, string version, string kind);

[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ManifestController : ControllerBase
{
    private readonly ILogger<ManifestController> _logger;
    private readonly IEtcdClient _etcdClient;

    public ManifestController(ILogger<ManifestController> logger, IEtcdClient etcdClient)
    {
        _logger = logger;
        _etcdClient = etcdClient;
    }

    [HttpPut(Name = "PutResourceManifest")]
    public async Task<IActionResult> PutManifestAsync(
        string group,
        string version,
        string kind,
        Manifest manifest,
        CancellationToken cancellationToken)
    {
        var keySpace = DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind));
        if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound(); }

        if (string.IsNullOrWhiteSpace(manifest?.Metadata?.Name))
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, "Metadata.Name missing or empty");
        }

        var etcdKey = Path.Combine(
            "/registry",
            DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind)),
            manifest?.Metadata?.Name ?? string.Empty);
        var etcdValue = JsonSerializer.Serialize(manifest);

        try
        {
            await _etcdClient.PutAsync(etcdKey, etcdValue, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }

        return Ok();
    }


    private static string DerriveEtcdKeyFromKind(ManifestRevision revision) =>
        revision switch
        {
            ("stocks.stockr.io", _, "stock") => "stocks/",
            ("stocks.stockr.io", _, "stocks") => "stocks/",
            _ => ""
        };
}
