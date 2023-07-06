using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddManifesto();
builder.Services.AddKeySpaces((string kind, string version, string group) => {
    return  ((kind, group, version)) switch {
        ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
        ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
        _ => string.Empty
    };
    
});
// builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapManifestApiControllers();

app.Run();
