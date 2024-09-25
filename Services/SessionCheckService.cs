using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AttendanceAPIV2.Models;
using Microsoft.EntityFrameworkCore;
using AttendanceAPIV2.Enums;
using System.Text;

public class SessionCheckService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public SessionCheckService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Log start of service
        Console.WriteLine("SessionCheckService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckSessionsAsync(stoppingToken); // Pass the cancellation token to allow graceful cancellation
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Task was canceled."); // Log cancellation
                break; // Exit the while loop if task is canceled
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

            // Delay of 5 minutes or until cancellation is requested
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Delay was canceled."); // Log cancellation
                break; // Exit the while loop if task is canceled
            }
        }

        // Log service stopping
        Console.WriteLine("SessionCheckService is stopping.");
    }

    private async Task CheckSessionsAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AttendanceContext>();

            var expiredSessions = await dbContext.Sessions
                .Include(s => s.AttendanceRecords)
                .Where(s => s.EndTime < DateTime.Now)
                .ToListAsync(stoppingToken); // Pass the cancellation token

            foreach (var session in expiredSessions)
            {
                if (session.Expired == ExpiredSession.NotExpired)
                {
                    foreach (var attendanceRecord in session.AttendanceRecords)
                    {
                        if (attendanceRecord.Status == AttendanceStatus.Absent)
                        {
                            var message = new StringBuilder();
                            message.AppendLine($"You are absent in session: {session.SessionName}");
                            var notification = new Notification
                            {
                                UserId = attendanceRecord.UserId,
                                Message = message.ToString(),
                                CreatedAt=DateTime.Now,
                                IsRead= false
                            };

                            dbContext.Notifications.Add(notification);
                        }
                    }
                    session.Expired = ExpiredSession.Expired;
                    dbContext.Sessions.Update(session);
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken); // Pass the cancellation token
        }
    }
}
