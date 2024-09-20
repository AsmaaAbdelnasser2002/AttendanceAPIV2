using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AttendanceAPIV2.Models
{
	public class SessionQrCode
	{
		[Key]
		public int QRCodeId { get; set; }

		public string Code { get; set; } // Unique code (possibly base64-encoded QR code)

		public DateTime GeneratedAt { get; set; }

		public DateTime ExpiresAt { get; set; }

		[ForeignKey("Session")]
		public int SessionId { get; set; }
		public virtual Session Session { get; set; }
	}
}
