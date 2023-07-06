// // using System.Text.Json;
// using dotnet_etcd.interfaces;
// using Microsoft.AspNetCore.Mvc;
// using Stockr.Api.Utilities;

// namespace Stockr.Api.Controllers;


// [ApiController]
// [Route("apis/{group}/{version}/{kind}")]
// public class ManifestController : ControllerBase
// {
//     private readonly ILogger<ManifestController> _logger;
//     private readonly IEtcdClient _etcdClient;

//     public ManifestController(ILogger<ManifestController> logger, IEtcdClient etcdClient)
//     {
//         _logger = logger;
//         _etcdClient = etcdClient;
//     }

//     [HttpPut(Name = "PutResourceManifest")]
//     public async Task<IActionResult> PutManifestAsync(
//         string group,
//         string version,
//         string kind,
//         Manifest manifest,
//         CancellationToken cancellationToken)
//     {
//         var keySpace = DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind));
//         if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound(); }

//         if (string.IsNullOrWhiteSpace(manifest?.Metadata?.Name))
//         {
//             return StatusCode(StatusCodes.Status422UnprocessableEntity, "Metadata.Name missing or empty");
//         }

//         manifest.ApiGroup = group;
//         manifest.ApiVersion = version;
//         manifest.Kind = kind;

//         var etcdKey = Path.Combine(
//             "/registry",
//             DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind)),
//             manifest?.Metadata?.Name ?? string.Empty);
//         var etcdValue = JsonSerializer.Serialize(manifest);

//         try
//         {
//             await _etcdClient.PutAsync(etcdKey, etcdValue, cancellationToken: cancellationToken);
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, "Failed to put resource mainfest to etcd.");
//             return StatusCode(StatusCodes.Status500InternalServerError, "Failed to persist the given manifest.");
//         }

//         return Ok();
//     }

//     [HttpGet("{name}", Name = "GetResourceByName")]
//     public async Task<IActionResult> GetManifestByName(
//         string group,
//         string version,
//         string kind,
//         string name,
//         CancellationToken cancellationToken)
//     {
//         var keySpace = DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind));
//         if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

//         var etcdKey = Path.Combine(
//             "/registry",
//             DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind)),
//             name);
//         try
//         {
//             var etcdResult = await _etcdClient.GetAsync(etcdKey, cancellationToken: cancellationToken);
//             if (etcdResult.Count < 1)
//             {
//                 return NotFound();
//             }

//             var result = etcdResult.Kvs.First();
//             var value = result.Value.ToStringUtf8();

//             var manifest = JsonSerializer.Deserialize<Manifest>(value);

//             if(manifest?.Metadata is null){
//                 manifest.Metadata = new Metadata();
//             }
//             manifest.Metadata.Revision = Math.Max(result.ModRevision, result.CreateRevision).ToString();

//             return Ok(manifest);
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, $"Failed to get resource {etcdKey}");
//             return StatusCode(StatusCodes.Status500InternalServerError, "Failed to read the given manifest.");
//         }
//     }
    
//     [HttpDelete("{name}", Name = "DeleteResourceByName")]
//     public async Task<IActionResult> DeleteManifestByName(
//         string group,
//         string version,
//         string kind,
//         string name,
//         CancellationToken cancellationToken)
//     {
//         var keySpace = DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind));
//         if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

//         var etcdKey = Path.Combine(
//             "/registry",
//             DerriveEtcdKeyFromKind(new ManifestRevision(group, version, kind)),
//             name);
//         try
//         {
//             var etcdResult = await _etcdClient.GetAsync(etcdKey, cancellationToken: cancellationToken);
//             if (etcdResult.Count < 1)
//             {
//                 return NotFound();
//             }

//             var result = etcdResult.Kvs.First();
//             var value = result.Value.ToStringUtf8();

//             var manifest = JsonSerializer.Deserialize<Manifest>(value);

//             if(manifest?.Metadata is null){
//                 manifest.Metadata = new Metadata();
//             }
//             manifest.Metadata.Revision = Math.Max(result.ModRevision, result.CreateRevision).ToString();
            
//             return Ok(manifest);
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, $"Failed to get resource {etcdKey}");
//             return StatusCode(StatusCodes.Status500InternalServerError, "Failed to read the given manifest.");
//         }
//     }
    
//     private static string DerriveEtcdKeyFromKind(ManifestRevision revision) =>
//         revision switch
//         {
//             ("stocks.stockr.io", _, "stock") => "stocks/",
//             ("stocks.stockr.io", _, "stocks") => "stocks/",
//             _ => ""
//         };
// }
