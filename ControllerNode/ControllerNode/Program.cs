using ControllerNode.Models;
using ControllerNode.Interfaces;
using ControllerNode.StorageNodes;
using ControllerNode.Services;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ControllerNode
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [STAThread]
        static async Task Main()
        {
            AllocConsole();

            // Leer configuración
            var config = new ConfigurationBuilder()
                .AddJsonFile("nodes.config.json", optional: false)
                .Build();

            int blockSize = config.GetValue<int>("BlockSize");
            var nodeInfos = config.GetSection("Nodes").Get<List<NodeInfo>>()!;

            var httpClient = new HttpClient();
            var nodes = nodeInfos
                .Select(info => new RemoteStorageNode(info.Id, info.Url, blockSize, httpClient))
                .Cast<IStorageNode>()
                .ToArray();

            var controller = new ControllerService(nodes, blockSize);

            

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(controller));
        }
    }
}
