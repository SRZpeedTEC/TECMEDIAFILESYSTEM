using ControllerNode.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerNode.StorageNodes
{
    public class InMemoryStorageNode : IStorageNode
    {
        private readonly List<byte[]?> blocks = new();
        public int Id { get; }
        public bool Online { get; set; } = true;

        public InMemoryStorageNode(int id) => Id = id;

        public Task<bool> IsOnlineAsync(CancellationToken _) => Task.FromResult(Online);

        public Task WriteBlockAsync(long index, byte[] data, CancellationToken _)
        {
            while (blocks.Count <= index) blocks.Add(null);
            blocks[(int)index] = data;
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadBlockAsync(long index, CancellationToken _)
        {
            if (!Online || index >= blocks.Count) return Task.FromResult<byte[]?>(null);
            return Task.FromResult(blocks[(int)index]);
        }

        public Task DeleteBlockAsync(long index, CancellationToken _)
        {
            if (index < blocks.Count) blocks[(int)index] = null;
            return Task.CompletedTask;
        }

    }

}
