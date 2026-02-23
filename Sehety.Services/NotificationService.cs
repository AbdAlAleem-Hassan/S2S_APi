using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using S2S.ServicesAbstraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Services
{
	public class NotificationService : INotificationService
	{
		private readonly ILogger<NotificationService> _logger;

		public NotificationService(ILogger<NotificationService> logger)
		{
			_logger = logger;
		}

		public async Task<bool> SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
		{
			try
			{
				// تجهيز الرسالة اللي هتروح لفايربيز
				var message = new Message()
				{
					Token = deviceToken, // الـ Token الخاص بموبايل اليوزر
					Notification = new Notification()
					{
						Title = title,
						Body = body
					},
					Data = data // بيانات إضافية مخفية الموبايل بيحتاجها (اختياري)
				};

				// إرسال الرسالة
				string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);

				_logger.LogInformation("Successfully sent message: {response}", response);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error sending FCM notification to token: {token}", deviceToken);
				return false;
			}
		}
	}
}
