using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using System;

namespace DurableFunctionSample
{
    class Phrase
    {
        [SimpleField(IsKey = true, IsFilterable = false, IsSortable = false, IsFacetable = false)]
        public string id { get; set; } = Guid.NewGuid().ToString();

        [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string account { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string container { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true, IsFacetable = false)]
        public string blob { get; set; }

        [SearchableField(IsFilterable = false, IsSortable = false, IsFacetable = false)]
        public string phrase { get; set; }

        [SimpleField(IsFilterable = false, IsSortable = false, IsFacetable = false)]
        public long offset { get; set; }
    }
}
