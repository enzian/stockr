
namespace Stockr.Api.Utilities;

public record ManifestRevision(string group, string version, string kind);

public static class EtcdKeyUtilities {
    public static string KeyFromKind(ManifestRevision revision) =>
        revision switch
        {
            ("stocks.stockr.io", _, "stock") => "stocks/",
            ("stocks.stockr.io", _, "stocks") => "stocks/",
            _ => ""
        };
}