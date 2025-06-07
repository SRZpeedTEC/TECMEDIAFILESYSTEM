
using DiskNode.Services;

{
    var builder = WebApplication.CreateBuilder(args);       // Host is created 
    builder.Configuration.AddXmlFile("StartUpXML.xml", optional: false, reloadOnChange: true);  // Configuration is loaded from XML file

    builder.Services.AddSingleton<BlockStorage>();
    builder.Services.AddControllers();
    var app = builder.Build();



    app.MapControllers();

    app.Run($"http://0.0.0.0:{builder.Configuration["DiskNode:Port"]}");

}
