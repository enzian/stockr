using Manifesto.AspNet;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddManifesto();
builder.Services.AddKeySpaces((string kind, string group, string version) => {
    return $"registry/{group}/{version}/{kind}";
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
