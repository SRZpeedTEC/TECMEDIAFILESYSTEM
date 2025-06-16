
using DiskNode.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// 1) Capturamos --config=RUTA_XML de los argumentos (o usamos StartUpXML.xml por defecto)
string configFile = args
    .FirstOrDefault(a => a.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
    ?.Substring("--config=".Length)
    .Trim('"')
    ?? "StartUpXML.xml";

Console.WriteLine($"[DiskNode] Cargando configuración desde: {configFile}");

// 2) Cargamos únicamente ese archivo XML
builder.Configuration.AddXmlFile(configFile, optional: false, reloadOnChange: true);

// 3) Registramos el servicio de almacenamiento y los controladores
builder.Services.AddSingleton<BlockStorage>();
builder.Services.AddControllers();

var app = builder.Build();

// 4) Mapeamos controladores
app.MapControllers();

// 5) Arrancamos en el puerto definido en la configuración cargada
var port = builder.Configuration["DiskNode:Port"];
Console.WriteLine($"[DiskNode] Ejecutando en http://0.0.0.0:{port}");
app.Run($"http://0.0.0.0:{port}");
