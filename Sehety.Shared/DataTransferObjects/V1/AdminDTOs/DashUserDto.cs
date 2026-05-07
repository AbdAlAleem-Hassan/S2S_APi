using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Shared.DataTransferObjects.V1.AdminDTOs
{
	public class DashUserDto
	{
		public string Id { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Email { get; set; }
		public bool IsLockedOut { get; set; } 
		public DateTimeOffset? LockoutEnd { get; set; }
	}
}
