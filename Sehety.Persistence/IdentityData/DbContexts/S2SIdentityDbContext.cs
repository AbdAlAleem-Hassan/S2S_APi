using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using S2S.Domain.Entities.IdentityModule;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace S2S.Persistence.IdentityData.DbContexts
{
	public class S2SIdentityDbContext : IdentityDbContext<ApplicationUser>
	{
		public S2SIdentityDbContext(DbContextOptions<S2SIdentityDbContext> options) : base(options)
		{
			
		}

		public DbSet<UserOtp> UserOtps { get; set; }
		public DbSet<UserPasswordHistory> UserPasswordHistories { get; set; }

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);
			builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly()); 
			builder.Entity<IdentityRole>().ToTable("Roles");
			builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
			
		}

	}
}
