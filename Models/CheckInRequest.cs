namespace AttendanceAPIV2.Models
{
    public class CheckInRequest
    {
        public int SessionId { get; set; }
        public string QRCodeData { get; set; }
        public string UserId { get; set; }
    }
}
