using System;
using System.Diagnostics;
using System.IO;

public static class DiskNodeLauncher
{
    // Ruta relativa desde ControllerNode/bin/... hasta el DLL de DiskNode
    private const string DiskNodeAppRelative =
        @"..\..\..\..\..\DiskNode\bin\Debug\net8.0\DiskNode.dll";

    private static readonly string DiskNodeApp = Path.GetFullPath(DiskNodeAppRelative);

    // Configuración (puerto, carpeta de datos) de cada uno de los 4 nodos
    private static readonly (int Port, string Path)[] nodeConfigs = new[] {
        (5001, @"..\..\..\..\..\DiskNode\storage\node1"),
        (5002, @"..\..\..\..\..\DiskNode\storage\node2"),
        (5003, @"..\..\..\..\..\DiskNode\storage\node3"),
        (5004, @"..\..\..\..\..\DiskNode\storage\node4"),
    };

    public static Process[] StartAllDiskNodes()
    {
        if (!File.Exists(DiskNodeApp))
            throw new FileNotFoundException($"No se encontró DiskNode.dll en '{DiskNodeApp}'");

        // Donde está el DLL
        string dllFolder = Path.GetDirectoryName(DiskNodeApp)!;
        if (!Directory.Exists(dllFolder))
            throw new DirectoryNotFoundException($"No existe el directorio '{dllFolder}'");

        var processes = new Process[nodeConfigs.Length];

        for (int i = 0; i < nodeConfigs.Length; i++)
        {
            var (port, path) = nodeConfigs[i];
            Directory.CreateDirectory(path);

            // RUTA COMPLETA AL StartUpXML.xml de tu proyecto DiskNode
            string xmlFile = @"..\..\..\..\..\DiskNode\StartUpXML.xml";

            // Argumentos para invocar dotnet
            string args =
                $"\"{DiskNodeApp}\" " +
                $"--config=\"{xmlFile}\" " +
                $"--DiskNode:Port={port} " +
                $"--DiskNode:Path=\"{path}\" " +
                "--DiskNode:TotalSizeMB=50 " +
                "--DiskNode:BlockSizeKB=4";

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args,
                WorkingDirectory = dllFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Opciones para silenciar telemetría y consejos HTTPS
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            int nodeNumber = i + 1;

            proc.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Node{nodeNumber}] {e.Data}");
            };
            proc.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[Node{nodeNumber} ERR] {e.Data}");
            };

            Console.WriteLine($"Arrancando DiskNode {nodeNumber} en puerto {port}, path={path}");
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            processes[i] = proc;
        }

        return processes;
    }

    public static void StopAllDiskNodes(Process[] processes)
    {
        foreach (var proc in processes)
            if (proc != null && !proc.HasExited)
                proc.Kill();
    }
}
