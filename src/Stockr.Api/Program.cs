using System.CommandLine;
using Manifesto.AspNet;

var clientKeyOption = new Option<int>
    (name: "--api-client-key",
    description: "Key used for client autentication",
    getDefaultValue: () => 42);
var clientCertificateOption = new Option<int>
    (name: "--api-client-certificate",
    description: "Client ",
    getDefaultValue: () => 42);

var rootCommand = new RootCommand("Parameter binding example");
rootCommand.Add(clientKeyOption);
rootCommand.Add(clientCertificateOption);


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddManifesto()
    .AddKeySpaces((string kind, string version, string group) => {
    return  ((kind, group, version)) switch {
        ("stock", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
        ("stocks", "logistics.stockr.io", "v1alpha1") => $"/registry/stocks",
        _ => string.Empty
    };
    
});

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

await app.RunAsync();
