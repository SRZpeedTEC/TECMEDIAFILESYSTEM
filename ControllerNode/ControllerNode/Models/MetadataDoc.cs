using System.Collections.Generic;

namespace ControllerNode.Models
{
	public class MetadataDoc
	{
		// Lista de metadatos de archivos
		public Dictionary<string, List<BlockRef>> FileTable { get; set; } = new();
		public Dictionary<string, int> FileSize { get; set; } = new();
	}
}
