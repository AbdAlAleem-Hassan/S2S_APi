using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using S2S.Domain.Entities.IdentityModule;
using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Persistence.IdentityData.DbContexts
{
	public class S2SIdentityDbContext : IdentityDbContext<ApplicationUser>
	{
		public S2SIdentityDbContext(DbContextOptions<S2SIdentityDbContext> options) : base(options)
		{
			
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);
			builder.Entity<Address>().ToTable("Addresses");
			builder.Entity<ApplicationUser>().ToTable("Users");
			builder.Entity<IdentityRole>().ToTable("Roles");
			builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
		}
	}
}
