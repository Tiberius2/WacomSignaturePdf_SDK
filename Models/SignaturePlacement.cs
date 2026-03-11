using WacomSignaturePdf.Models;

namespace WacomSignaturePdf.Models
{
    public class SignaturePlacement
    {
        public int SignatureId { get; set; }
        public string Party { get; set; }
        public SignatureCapture Capture { get; set; }
        public int Page { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string FssPath { get; set; }
    }
}