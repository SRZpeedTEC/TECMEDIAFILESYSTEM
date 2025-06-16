using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ControllerNode
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main() // Cambiar el tipo de retorno a Task y agregar el modificador async
        {
            AllocConsole(); // Crea y muestra la consola

            var processes = DiskNodeLauncher.StartAllDiskNodes();
            await Task.Delay(2000);             // opcional: espera a que arranquen
            var controller = new ControllerNodeHTTPS();
            controller.Test();
            DiskNodeLauncher.StopAllDiskNodes(processes);

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
