using Newtonsoft.Json;

namespace WacomSignaturePdf.Models
{
    public class SignatureSlot
    {
        [JsonProperty("SignatureId")] public int SignatureId { get; set; }
        [JsonProperty("SignerName")] public string SignerName { get; set; }
        [JsonProperty("Reason")] public string Reason { get; set; }
        [JsonProperty("Page")] public string Page { get; set; }
        [JsonProperty("Party")] public string Party { get; set; } // Candidate or official
        [JsonProperty("Location")] public SignatureLocation Location { get; set; } 
        [JsonProperty("Required")] public bool Required { get; set; }
        [JsonProperty("Biometric")] public bool Biometric { get; set; }


        // Resolved at load time — not serialized
        [JsonIgnore] public string ResolvedSignerName { get; set; }
        [JsonIgnore] public int ResolvedPage { get; set; }
    }

    public class SignatureLocation
    {
        [JsonProperty("X")] public float X { get; set; }
        [JsonProperty("Y")] public float Y { get; set; }
        [JsonProperty("W")] public float W { get; set; }
        [JsonProperty("H")] public float H { get; set; }
    }
}