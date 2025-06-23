using ControllerNode.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;


//Permiten la comunicacion de http con los disk nodes 
namespace ControllerNode.StorageNodes
{
    public sealed class RemoteStorageNode : IStorageNode
    {
        private readonly HttpClient _http;

        private readonly string _baseUrl;
        private readonly int _blockSize;
        public int Id { get; }

        // Constructor que inicializa el nodo remoto con su ID, URL base y tamaño de bloque
        public RemoteStorageNode(int id, string url, int blockSize, HttpClient http)
        {
            Id = id;
            _baseUrl = url.TrimEnd('/');
            _blockSize = blockSize;
            _http = http;

        }

        // Comprueba si el nodo está en línea
        public async Task<bool> IsOnlineAsync(CancellationToken ct)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/blocks/health", ct);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // Escribe un bloque de datos en el nodo remoto
        public async Task WriteBlockAsync(long index, byte[] data, CancellationToken ct)
        {
            if (data.Length != _blockSize)
            {
                throw new ArgumentException($"Block must be exactly {_blockSize} bytes");
            }

            // Crea el contenido del bloque y establece los encabezados necesarios

            using var content = new ByteArrayContent(data);
            content.Headers.ContentLength = _blockSize;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            // Realiza la solicitud PUT para escribir el bloque en el nodo remoto
            var resp = await _http.PutAsync($"{_baseUrl}/blocks/{index}", content, ct);
            resp.EnsureSuccessStatusCode();
        }

        // Lee un bloque de datos del nodo remoto
        public async Task<byte[]?> ReadBlockAsync(long index, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            // Realiza la solicitud GET para leer el bloque del nodo remoto
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/blocks/{index}", cts.Token);
                if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync(cts.Token);
            }
            catch (TaskCanceledException) { return null; }
        }

        // Elimina un bloque de datos del nodo remoto
        public async Task DeleteBlockAsync(long index, CancellationToken ct)
        {
            var resp = await _http.DeleteAsync($"{_baseUrl}/blocks/{index}", ct);
            resp.EnsureSuccessStatusCode();
        }
    }
}
