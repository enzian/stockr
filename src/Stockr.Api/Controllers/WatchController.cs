// using System.Text.Json;
// using dotnet_etcd.interfaces;
// using Etcdserverpb;
// using Google.Protobuf;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Mvc.Filters;
// using Stockr.Api.Utilities;
// using static Mvccpb.Event.Types;

// namespace Stockr.Api.Controllers;

// [ApiController]
// [Route("apis/watch/{group}/{version}/{kind}")]
// public class WatchController : ControllerBase
// {
//     private readonly ILogger<ManifestController> _logger;
//     private readonly IEtcdClient _etcdClient;

//     public WatchController(ILogger<ManifestController> logger, IEtcdClient etcdClient)
//     {
//         _logger = logger;
//         _etcdClient = etcdClient;
//     }

//     [HttpGet(Name = "WatchResourceKind")]
//     public async Task<IActionResult> WatchResourceKind(
//         string group,
//         string version,
//         string kind,
//         [FromQuery] string? start_revision,
//         [FromQuery] string? filter,
//         CancellationToken cancellationToken)
//     {
//         var context = HttpContext;
//         var keySpace = EtcdKeyUtilities.KeyFromKind(new ManifestRevision(group, version, kind));
//         if (string.IsNullOrWhiteSpace(keySpace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

//         var etcdKey = Path.Combine(
//             "/registry",
//             EtcdKeyUtilities.KeyFromKind(new ManifestRevision(group, version, kind)));

//         var filters = filter is not null ? Selectors.TryParse(filter) : new[] { new Selector.None() };

//         try
//         {
//             context.Response.StatusCode = StatusCodes.Status200OK;
//             context.Response.ContentType = "text/event-stream";

//             await _etcdClient.WatchAsync(
//                 new WatchRequest
//                 {
//                     CreateRequest = new WatchCreateRequest
//                     {
//                         Key = dotnet_etcd.EtcdClient.GetStringByteForRangeRequests(etcdKey),
//                         RangeEnd = ByteString.CopyFromUtf8(dotnet_etcd.EtcdClient.GetRangeEnd(etcdKey)),
//                         ProgressNotify = true,
//                         StartRevision = start_revision != null ? long.Parse(start_revision) : 0
//                     }
//                 },
//                 async (response) => await WatchController.HandleReponse(response, filters, context, cancellationToken),
//                 new Grpc.Core.Metadata(),
//                 cancellationToken: cancellationToken);

//             return Ok();
//         }
//         catch (Exception e)
//         {
//             _logger.LogError(e, $"Failed to get resource {etcdKey}");
//             return StatusCode(StatusCodes.Status500InternalServerError, "Failed to read the given manifest.");
//         }
//     }

//     private static async Task HandleReponse(
//         WatchResponse response,
//         IEnumerable<Selector>? filters,
//         HttpContext context,
//         CancellationToken cancellationToken)
//     {
//         {
//             var revision = response.Header.Revision;
//             foreach (var e in response.Events)
//             {
//                 var manifest = JsonSerializer.Deserialize<Manifest>(e.Kv.Value.ToStringUtf8());
//                 if (manifest?.Metadata != null)
//                 {
//                     manifest.Metadata.Revision = revision.ToString();
//                 }
//                 else if (manifest != null)
//                 {
//                     manifest.Metadata = new Metadata { Revision = revision.ToString() };
//                 }

//                 if (Selectors.Validate(filters, manifest.Metadata.Labels))
//                 {
//                     var watchEvent = e switch
//                     {
//                         { Type: EventType.Put, Kv.Version: 1 } => new WatchEvent { Type = "ADDED", Object = manifest },
//                         { Type: EventType.Put, Kv.Version: > 1 } => new WatchEvent { Type = "MODIFIED", Object = manifest },
//                         { Type: EventType.Delete } => new WatchEvent { Type = "DELETED", Object = manifest },
//                     };

//                     await context.Response.WriteAsync($"{JsonSerializer.Serialize(watchEvent)}{Environment.NewLine}", cancellationToken);
//                 }

//             }

//             await context.Response.Body.FlushAsync();
//         }
//     }
// }
