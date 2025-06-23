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

       
        Task<bool> IsOnlineAsync(CancellationToken ct); // Responde al health-check
                                                      
        Task WriteBlockAsync(long index, byte[] data, CancellationToken ct); // Escribe el bloque exacto segun la configuracion
   
        Task<byte[]?> ReadBlockAsync(long index, CancellationToken ct); // Devuelve el contenido

        Task DeleteBlockAsync(long index, CancellationToken ct); // Elimina el bloque, si existe


    }
}
