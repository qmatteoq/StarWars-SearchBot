using Azure.Search.Documents.Indexes;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SearchFunction.Models
{
    public class DatabankEntry
    {
        [JsonPropertyName("content")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Content { get; set; }

        [JsonPropertyName("people")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public List<string> People { get; set; }

        [JsonPropertyName("locations")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public List<string> Locations { get; set; }

        [JsonPropertyName("metadata_storage_name")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string FileName { get; set; }

        [JsonPropertyName("metadata_storage_path")]
        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Path { get; set; }

        [JsonPropertyName("index_key")]
        [SearchableField(IsKey = true, IsFilterable = true, IsSortable = true)]
        public string Id { get; set; }

    }
}
