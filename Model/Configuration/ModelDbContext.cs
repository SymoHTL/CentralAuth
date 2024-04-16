using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Model.Entities;

namespace Model.Configuration;

public class ModelDbContext(DbContextOptions<ModelDbContext> options) :
    IdentityDbContext<AppUser>(options) {
    public DbSet<CorsOrigin> CorsOrigins{ get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder) {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ModelDbContext).Assembly);
    }
}