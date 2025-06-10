using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ControllerNode.Interfaces;
using ControllerNode.Services;
using ControllerNode.StorageNodes;

namespace ControllerNode
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [STAThread]
        static async Task Main()
        {
            AllocConsole(); // Habilita consola para depurar

            // ====== Configuración base ======
            int blockSize = 4;

            // Crear nodos en memoria
            var nodes = Enumerable.Range(0, 4)
                                  .Select(i => (IStorageNode)new InMemoryStorageNode(i))
                                  .ToArray();

            // Instanciar servicio RAID
            var controller = new ControllerService(nodes, blockSize);

            // ====== Prueba de integración mínima ======
            string fileName = "Documento1";
            string contenido = "Hola, este es un archivo de prueba para RAID5.";
            byte[] datos = Encoding.UTF8.GetBytes(contenido);

            await controller.AddDocumentAsync(fileName, datos);
            Console.WriteLine("[✔] Archivo agregado");

            var resultado = await controller.GetDocumentAsync(fileName);
            Console.WriteLine("[✔] Recuperado: " + Encoding.UTF8.GetString(resultado));

            ((InMemoryStorageNode)nodes[0]).Online = false;
            Console.WriteLine("[!] Nodo 0 OFFLINE");

            var recuperado2 = await controller.GetDocumentAsync(fileName);
            Console.WriteLine("[✔] Con nodo caído: " + Encoding.UTF8.GetString(recuperado2));

            await controller.RemoveDocumentAsync(fileName);
            Console.WriteLine("[✔] Archivo eliminado");

            var resultado3 = await controller.GetDocumentAsync(fileName);
            Console.WriteLine(resultado3 == null ? "[✔] Ya no existe el archivo" : "[X] El archivo aún existe");

            // ====== Inicializar UI (aunque aún no se use) ======
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());  // ← aquí irá la interfaz real más adelante
        }
    }
}
