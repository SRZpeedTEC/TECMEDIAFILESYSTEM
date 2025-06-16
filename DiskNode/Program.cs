using DiskNode.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// 1) Extrae de args el parámetro --config=RUTA (sin depender de AddCommandLine)
string configArg = args
  .FirstOrDefault(a => a.StartsWith("--config=", StringComparison.OrdinalIgnoreCase));
string configFile = configArg != null
  ? configArg.Substring("--config=".Length).Trim('"')
  : "StartUpXML.xml";

Console.WriteLine($"[DiskNode] Cargando configuración desde: {configFile}");

// 2) Carga únicamente ese XML
builder.Configuration.AddXmlFile(configFile, optional: false, reloadOnChange: true);

// 3) Registra el servicio de almacenamiento y MVC
builder.Services.AddSingleton<BlockStorage>();
builder.Services.AddControllers();

var app = builder.Build();

// 4) Mapea controladores y arranca en el puerto de la config
app.MapControllers();
var port = builder.Configuration["DiskNode:Port"];
Console.WriteLine($"[DiskNode] Ejecutando en http://0.0.0.0:{port}");
app.Run($"http://0.0.0.0:{port}");
