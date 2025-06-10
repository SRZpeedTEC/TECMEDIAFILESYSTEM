using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerNode.Models
{

    public record BlockRef(
        string FileName,
        int BlockNumber,   // 0-based dentro del archivo
        int StripeIndex,   // franja RAID
        bool IsParity,      // true = bloque de paridad
        int NodeIndex,     // a qué nodo fue
        int NodeBlockIndex // índice dentro del nodo
    );
}
