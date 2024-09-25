using AttendanceAPIV2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceAPIV2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly AttendanceContext _context;

        public NotificationController(AttendanceContext context)
        {
            _context = context;
        }

        // Retrieve all notifications for a specific user
        [HttpPost("GetAllNotification")]
        public  IActionResult GetUserNotificationsAsync(string userId)
        {

            var notification =  _context.Notifications
                                .Where(n => n.UserId == userId)
                                .OrderByDescending(n => n.CreatedAt)
                                .ToList();

            return Ok(notification);
        }

        //open a notification as read
        [HttpPost("OpenNotification")]
        public IActionResult MarkNotificationAsReadAsync(int notificationId)
        {
            var notification =  _context.Notifications.Find(notificationId);

            if (notification != null && notification.IsRead == false)
            {
                notification.IsRead = true;
                 _context.SaveChangesAsync();
            }
            return Ok(notification);
        }

    }
}
