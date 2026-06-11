using Newtonsoft.Json;
using System.Collections.Generic;

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
        [JsonProperty("FilePattern")] public string FilePattern { get; set; }

        [JsonIgnore] public bool IsMultiDocument => !string.IsNullOrWhiteSpace(FilePattern);
    }
}