using AttendanceAPIV2.Models;

public class QRCodeRegenerationService : BackgroundService
{
	private readonly IServiceScopeFactory _serviceScopeFactory;

	public QRCodeRegenerationService(IServiceScopeFactory serviceScopeFactory)
	{
		_serviceScopeFactory = serviceScopeFactory;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			using (var scope = _serviceScopeFactory.CreateScope())
			{
				var qrCodeService = scope.ServiceProvider.GetRequiredService<QRCodeService>(); // Resolve QRCodeService within the scope
				var dbContext = scope.ServiceProvider.GetRequiredService<AttendanceContext>();

				var activeSessions = dbContext.Sessions
					.Where(s => s.StartTime <= DateTime.Now && s.EndTime >= DateTime.Now)
					.ToList();

				foreach (var session in activeSessions)
				{
					var newQRCode = qrCodeService.GenerateQRCode($"{session.SessionId}-{Guid.NewGuid()}");

					var sessionQRCode = new SessionQrCode
					{
						SessionId = session.SessionId,
						Code = newQRCode,
						GeneratedAt = DateTime.Now,
						ExpiresAt = DateTime.Now.AddMinutes(5)
					};

					// Remove old QR codes
					var oldQRCodes = dbContext.SessionQRCodes
						.Where(q => q.SessionId == session.SessionId && q.ExpiresAt <= DateTime.Now);

					dbContext.SessionQRCodes.RemoveRange(oldQRCodes);
					dbContext.SessionQRCodes.Add(sessionQRCode);
					await dbContext.SaveChangesAsync();
				}
			}

			// Wait for 5 minutes before generating new QR codes
			await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
		}
	}
}
