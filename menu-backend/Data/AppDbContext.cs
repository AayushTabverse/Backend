using menu_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace menu_backend.Data;

public class AppDbContext : DbContext
{
    private readonly string? _tenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantId = tenantProvider.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemModifier> MenuItemModifiers => Set<MenuItemModifier>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<WebsiteContent> WebsiteContents => Set<WebsiteContent>();
    public DbSet<CustomerDue> CustomerDues => Set<CustomerDue>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Global query filters for multi-tenant isolation ──
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<RestaurantTable>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<MenuCategory>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<MenuItem>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<MenuItemModifier>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<PrintJob>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<WebsiteContent>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<CustomerDue>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));
        modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));

        // ── Tenant ──
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(e => e.TenantId).IsUnique();
        });

        // ── User ──
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.Users)
                  .HasForeignKey(e => e.TenantId)
                  .HasPrincipalKey(t => t.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RestaurantTable ──
        modelBuilder.Entity<RestaurantTable>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TableNumber }).IsUnique();
            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.Tables)
                  .HasForeignKey(e => e.TenantId)
                  .HasPrincipalKey(t => t.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── MenuCategory ──
        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.HasOne(e => e.Tenant)
                  .WithMany(t => t.MenuCategories)
                  .HasForeignKey(e => e.TenantId)
                  .HasPrincipalKey(t => t.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── MenuItem ──
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Items)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MenuItemModifier ──
        modelBuilder.Entity<MenuItemModifier>(entity =>
        {
            entity.Property(e => e.AdditionalPrice).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.MenuItem)
                  .WithMany(m => m.Modifiers)
                  .HasForeignKey(e => e.MenuItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Order ──
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.OrderNumber }).IsUnique();
            entity.Property(e => e.SubTotal).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Tax).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Table)
                  .WithMany(t => t.Orders)
                  .HasForeignKey(e => e.TableId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── OrderItem ──
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.MenuItem)
                  .WithMany()
                  .HasForeignKey(e => e.MenuItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Payment ──
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasOne(e => e.Order)
                  .WithOne(o => o.Payment)
                  .HasForeignKey<Payment>(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PrintJob ──
        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.PrintJobs)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CustomerDue ──
        modelBuilder.Entity<CustomerDue>(entity =>
        {
            entity.Property(e => e.BillAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.DueAmount).HasColumnType("decimal(10,2)");
            entity.HasIndex(e => new { e.TenantId, e.CustomerMobile });
        });
    }

    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(_tenantId) && string.IsNullOrEmpty(entry.Entity.TenantId))
                    entry.Entity.TenantId = _tenantId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}

/// <summary>
/// Provides the current tenant context, resolved from JWT claims or request headers.
/// </summary>
public interface ITenantProvider
{
    string? TenantId { get; }
}

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? TenantId
    {
        get
        {
            // First check JWT claim
            var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantClaim)) return tenantClaim;

            // Fallback to header
            var headerTenant = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerTenant)) return headerTenant;

            // Fallback to query string
            return _httpContextAccessor.HttpContext?.Request.Query["tenantId"].FirstOrDefault();
        }
    }
}
