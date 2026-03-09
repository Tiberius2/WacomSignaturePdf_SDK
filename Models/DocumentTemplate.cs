using System.Collections.Generic;
using Newtonsoft.Json;

namespace WacomSignaturePdf.Models
{
    public class DocumentTemplate
    {
        [JsonProperty("TemplateId")] public string TemplateId { get; set; }
        [JsonProperty("TemplateName")] public string TemplateName { get; set; }
        [JsonProperty("FileSystemBlock")] public FileSystemBlock FileSystemBlock { get; set; }
        [JsonProperty("Signatures")] public List<SignatureSlot> Signatures { get; set; }
    }

    public class FileSystemBlock
    {
        [JsonProperty("InputFileName")] public string InputFileName { get; set; }
        [JsonProperty("OutputFileName")] public string OutputFileName { get; set; }
    }
}