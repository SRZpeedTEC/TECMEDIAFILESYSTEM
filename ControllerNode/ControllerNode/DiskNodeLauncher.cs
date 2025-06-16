using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

public static class DiskNodeLauncher
{
    // Ruta al ejecutable DiskNode.dll (ajústala a tu proyecto)
    private const string DiskNodeAppRelative =
        @"..\..\..\..\..\DiskNode\bin\Debug\net8.0\DiskNode.dll";
    private static readonly string DiskNodeApp = Path.GetFullPath(DiskNodeAppRelative);

    // Configuración de cada nodo
    private static readonly (int Port, string Path)[] nodeConfigs = {
        (5001, @"..\..\..\..\..\DiskNode\storage\node1"),
        (5002, @"..\..\..\..\..\DiskNode\storage\node2"),
        (5003, @"..\..\..\..\..\DiskNode\storage\node3"),
        (5004, @"..\..\..\..\..\DiskNode\storage\node4"),
    };

    public static Process[] StartAllDiskNodes()
    {
        if (!File.Exists(DiskNodeApp))
            throw new FileNotFoundException($"No se encontró DiskNode.dll en '{DiskNodeApp}'");

        var dllFolder = Path.GetDirectoryName(DiskNodeApp)!;
        if (!Directory.Exists(dllFolder))
            throw new DirectoryNotFoundException($"No existe el directorio '{dllFolder}'");

        var processes = new Process[nodeConfigs.Length];

        for (int i = 0; i < nodeConfigs.Length; i++)
        {
            var (port, path) = nodeConfigs[i];
            Directory.CreateDirectory(path);

            // 1) Generar un XML de configuración dinámico
            var configXml = new XDocument(
                new XElement("settings",
                    new XElement("DiskNode",
                        new XElement("Port", port),
                        new XElement("Path", path),
                        new XElement("TotalSizeMB", 50),
                        new XElement("BlockSizeKB", 4)
                    )
                )
            );
            // Guardar en un archivo temporal para este nodo
            string xmlFile = Path.Combine(dllFolder, $"StartUpXML_Node{i + 1}.xml");
            configXml.Save(xmlFile);

            // 2) Lanzar DiskNode.dll pasando --config a ese XML
            string args =
                $"\"{DiskNodeApp}\" " +
                $"--config=\"{xmlFile}\"";

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
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            int nodeNumber = i + 1;
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[Node{nodeNumber}] {e.Data}"); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine($"[Node{nodeNumber} ERR] {e.Data}"); };

            Console.WriteLine($"Arrancando DiskNode {nodeNumber} con XML '{xmlFile}'");
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
