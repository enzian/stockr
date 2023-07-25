using Manifesto.AspNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;

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