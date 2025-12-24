using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Domain.Entities.IdentityModule
{
	public class Address
	{
		public int Id { get; set; }
		public string City { get; set; }
		public string Street { get; set; }
		public string Country { get; set; }

		public ApplicationUser User { get; set; }
		public string UserId { get; set; }
	}
}
