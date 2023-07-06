using dotnet_etcd;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Etcd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Manifesto.AspNet.Controllers;
using Manifesto.AspNet.Manifests;

namespace Manifesto.AspNet;

public static class WebApplicationBuilderExtensions
{
    public static IServiceCollection AddManifesto(this IServiceCollection subject) {
        subject.AddTransient<IManifestRepository, EtcdManifestRepository>();
        subject.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"));

        var assembly = typeof(ManifestV1CreationController).Assembly;
        subject.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(assembly));
        subject.AddKeySpaces((string kind, string version, string group) => $"{group}/{version}/{kind}");
        return subject;
    }
    
    public static IServiceCollection AddKeySpaces(this IServiceCollection subject, Func<string, string, string, string> keyspaceFunction) {
        return subject.AddSingleton<IKeyspaceSource>(new DelegateKeyspaceSource(keyspaceFunction));
    }

    public static void MapManifestApiControllers(this IEndpointRouteBuilder endpoints){
        endpoints.MapControllers();
    }
        
}
