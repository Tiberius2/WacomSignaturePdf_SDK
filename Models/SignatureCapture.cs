using System;

namespace WacomSignaturePdf.Models
{
    /// <summary>
    /// Captures all metadata produced during a single biometric signature capture.
    /// SigText is a Base64-encoded FSS blob — persist this to the database.
    /// </summary>
    public class SignatureCapture
    {
        public string SigText { get; set; }   // Base64 FSS — persist to DB
        public string DocumentHash { get; set; }   // SHA-256 of PDF at capture time
        public string SignerName { get; set; }
        public string Reason { get; set; }
        public DateTime CapturedAt { get; set; }
        public DateTime? TrustedAt { get; set; }   // TSA-verified UTC time, null if TSA unavailable
        public string ArtifactDir { get; set; }
        public string ImagePath { get; set; }   // Composite PNG (temp file)
    }

    /// <summary>
    /// Associates a SignatureCapture with its target position in the PDF.
    /// </summary>
    public class SignaturePlacement
    {
        public SignatureCapture Capture { get; set; }
        public int Page { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string FssPath { get; set; }  // Binary FSS temp file — attached to PDF
    }
}