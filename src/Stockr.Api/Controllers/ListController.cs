using System.Text.Json;
using dotnet_etcd.interfaces;
using Microsoft.AspNetCore.Mvc;
using Stockr.Api.Utilities;

namespace Stockr.Api.Controllers;

[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ListController : ControllerBase
{
    private readonly ILogger<ManifestController> _logger;
    private readonly IEtcdClient _etcdClient;

    public ListController(ILogger<ManifestController> logger, IEtcdClient etcdClient)
    {
        _logger = logger;
        _etcdClient = etcdClient;
    }

    [HttpGet(Name = "ListResourceManifests")]
    public async Task<IActionResult> ListResourceManifests(
        string group,
        string version,
        string kind,
        CancellationToken cancellationToken)
    {
        var keySpace = EtcdKeyUtilities.KeyFromKind(new ManifestRevision(group, version, kind));
        if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound(); }


        var etcdKey = Path.Combine(
            "/registry",
            EtcdKeyUtilities.KeyFromKind(new ManifestRevision(group, version, kind)));
        try
        {
            var rangeResponse = await _etcdClient.GetRangeAsync(etcdKey, cancellationToken: cancellationToken);
            var manifests = rangeResponse.Kvs.Select(x => {
                var manifest = JsonSerializer.Deserialize<Manifest>(x.Value.ToStringUtf8());
                if(manifest?.Metadata is null){
                    manifest.Metadata = new Metadata();
                }
                manifest.Metadata.Revision = Math.Max(x.ModRevision, x.CreateRevision).ToString();
                return manifest;
            });
            
            return Ok(manifests);            
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to put resource mainfest to etcd.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to persist the given manifest.");
        }
    }
}
