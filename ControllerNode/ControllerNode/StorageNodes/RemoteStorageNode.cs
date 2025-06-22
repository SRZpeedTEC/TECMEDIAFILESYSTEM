using ControllerNode.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ControllerNode.StorageNodes
{
    public sealed class RemoteStorageNode : IStorageNode
    {
        private readonly HttpClient _http;

        private readonly string _baseUrl;
        private readonly int _blockSize;
        public int Id { get; }

        public RemoteStorageNode(int id, string url, int blockSize, HttpClient http)
        {
            Id = id;
            _baseUrl = url.TrimEnd('/');
            _blockSize = blockSize;
            _http = http;

        }

        public async Task<bool> IsOnlineAsync(CancellationToken ct)
        {
            try
            {
                var r = await _http.GetAsync($"{_baseUrl}/blocks/health", ct);
                return r.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task WriteBlockAsync(long index, byte[] data, CancellationToken ct)
        {
            if (data.Length != _blockSize)
                throw new ArgumentException($"Block must be exactly {_blockSize} bytes");

            using var content = new ByteArrayContent(data);
            content.Headers.ContentLength = _blockSize;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var resp = await _http.PutAsync($"{_baseUrl}/blocks/{index}", content, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task<byte[]?> ReadBlockAsync(long index, CancellationToken ct)
        {
            var resp = await _http.GetAsync($"{_baseUrl}/blocks/{index}", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
    }
}
