using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Persistence.DbContexts
{
	public class S2SDbContext : DbContext
	{
		public S2SDbContext(DbContextOptions<S2SDbContext> options):base(options)
		{
			
		}
	}
}
