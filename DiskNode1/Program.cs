
using DiskNode.Services;

{
    var builder = WebApplication.CreateBuilder(args);       // Host is created 

    builder.Configuration.AddXmlFile("disk1.xml", optional: false, reloadOnChange: true);  // Configuration is loaded from XML file
     
    /*
    // Validar que se pasó un archivo XML por argumento
    if (args.Length == 0)
    {
        Console.WriteLine("ERROR: Debes proporcionar el nombre del archivo XML como argumento.");
        return;
    }
    string xmlFile = args[0]; // Ejemplo: "disk1.xml"

    try
    {
        builder.Configuration.AddXmlFile(xmlFile, optional: false, reloadOnChange: true);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR al cargar el archivo XML '{xmlFile}': {ex.Message}");
        return;
    }
    */

    builder.Services.AddSingleton<BlockStorage>();
    builder.Services.AddControllers();
    var app = builder.Build();



    app.MapControllers();

    app.Run($"http://0.0.0.0:{builder.Configuration["DiskNode:Port"]}");

}
