using ControllerNode.Models;
using ControllerNode.Interfaces;
using ControllerNode.StorageNodes;
using ControllerNode.Services;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;



namespace ControllerNode
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [STAThread]
        static async Task Main(string[] args)// recibir argumentos del powershell
        {
            AllocConsole();

            // Leer configuración
            var config = new ConfigurationBuilder()
                .AddJsonFile("nodes.config.json", optional: false)
                .Build();
            //--------------------------------------
            string host = "0.0.0.0"; // valor por defecto
            int apiPort = config.GetValue<int>("Port", 6000); // desde config

            if (args.Length >= 2)
            {
                host = args[0];
                if (!int.TryParse(args[1], out apiPort))
                {
                    Console.WriteLine("El puerto ingresado no es válido. Usando el del archivo de configuración.");
                }
            }

            //--------------------------------------

            int blockSize = config.GetValue<int>("BlockSize");
            var nodeInfos = config.GetSection("Nodes").Get<List<NodeInfo>>()!;

            var httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan   // ⬅ sin límite
            };


            var nodes = nodeInfos
                .Select(info => new RemoteStorageNode(info.Id, info.Url, blockSize, httpClient))
                .Cast<IStorageNode>()
                .ToArray();

            var controller = new ControllerService(nodes, blockSize);

            var builder = WebApplication.CreateBuilder();          
            builder.Services.AddSingleton(controller);          // inyecta nuestro servicio
            builder.Services.AddControllers();                     // recogera DocumentsController
            var app = builder.Build();
            app.MapControllers();

            // Levanta Kestrel **sin bloquear** (RunAsync)
            var webTask = app.RunAsync($"http://{host}:{apiPort}");


            Console.WriteLine($"[ControllerNode] API escuchando en http://localhost:{apiPort}");

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(controller));

            await app.StopAsync();
        }
    }
}
