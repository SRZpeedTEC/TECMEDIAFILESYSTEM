using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerNode.Interfaces
{
    public interface IStorageNode
    {
        int Id { get; }

        /// <summary>¿El nodo responde al *health-check*?</summary>
        Task<bool> IsOnlineAsync(CancellationToken ct);

        /// <summary>Escribe un bloque EXACTO del tamaño configurado.</summary>
        Task WriteBlockAsync(long index, byte[] data, CancellationToken ct);

        /// <summary>Lee un bloque; devuelve null si no existe.</summary>
        Task<byte[]?> ReadBlockAsync(long index, CancellationToken ct);

        Task DeleteBlockAsync(long index, CancellationToken ct);


    }
}
