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
        static void Main()
        {
            AllocConsole(); // Crea y muestra la consola

            ControllerNodeProject controller = new ControllerNodeProject();
            controller.Test();

            // Para personalizar configuraciones de la aplicación (DPI, fuente, etc.)
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
