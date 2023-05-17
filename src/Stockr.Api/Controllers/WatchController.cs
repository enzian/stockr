using System.Text.Json;
using dotnet_etcd.interfaces;
using Etcdserverpb;
using Microsoft.AspNetCore.Mvc;

namespace Stockr.Api.Controllers;

[ApiController]
[Route("apis/watch/{group}/{version}/{kind}")]
public class WatchController : ControllerBase
{
    private readonly ILogger<ManifestController> _logger;
    private readonly IEtcdClient _etcdClient;

    public WatchController(ILogger<ManifestController> logger, IEtcdClient etcdClient)
    {
        _logger = logger;
        _etcdClient = etcdClient;
    }

    [HttpGet(Name = "WatchResourceKind")]
    public async Task<IActionResult> GetManifestByName(
        string group,
        string version,
        string kind,
        CancellationToken cancellationToken)
    {
        var context = HttpContext;
        var keySpace = DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind));
        if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

        var etcdKey = Path.Combine(
            "/registry",
            DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind)));

        try
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";

            await _etcdClient.WatchRangeAsync(etcdKey, async (WatchResponse resposne) =>
            {
                var revision = resposne.Header.Revision;
                foreach (var e in resposne.Events)
                {
                    var manifest = JsonSerializer.Deserialize<Manifest>(e.Kv.Value.ToStringUtf8());
                    if (manifest?.Metadata != null)
                    {
                        manifest.Metadata.Revision = revision.ToString();
                    }
                    else if (manifest != null)
                    {
                        manifest.Metadata = new Metadata { Revision = revision.ToString() };
                    }

                    await context.Response.WriteAsync($"{JsonSerializer.Serialize(manifest)}{Environment.NewLine}");
                }


                // await context.Response.WriteAsync($"{dgram}\n", cancellationToken);
                // await context.Response.WriteAsJsonAsync(events, cancellationToken);
                await context.Response.Body.FlushAsync();
                // Console.WriteLine($"Flushed {dgram}");
            }, cancellationToken: cancellationToken);

            return Ok();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to get resource {etcdKey}");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to read the given manifest.");
        }
    }

    private static string DerriveEtcdKeyFromKind(ManifestRevision revision) =>
        revision switch
        {
            ("stocks.stockr.io", _, "stock") => "stocks/",
            ("stocks.stockr.io", _, "stocks") => "stocks/",
            _ => ""
        };
}
