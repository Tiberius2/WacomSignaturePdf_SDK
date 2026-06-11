using System;

namespace WacomSignaturePdf.Models
{
    // All metadata produced during a single biometric signature capture.
    // SigText is a Base64-encoded FSS blob — persist this to the database.
    public class SignatureCapture
    {
        public string SigText { get; set; }         // Base64 FSS blob
        public string DocumentHash { get; set; }    // SHA-256 of PDF at capture time
        public string SignerName { get; set; }
        public string Reason { get; set; }
        public DateTime CapturedAt { get; set; }
        public DateTime? TrustedAt { get; set; }    // TSA-verified time; null if TSA unavailable
        public string ArtifactDir { get; set; }
        public string ImagePath { get; set; }       // Composite PNG (temp file)
    }
}