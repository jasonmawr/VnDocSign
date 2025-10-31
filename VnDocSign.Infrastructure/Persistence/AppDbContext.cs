using Microsoft.EntityFrameworkCore;
using VnDocSign.Domain.Entities;
using VnDocSign.Domain.Entities.Config;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Domain.Entities.Documents;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Dossier> Dossiers => Set<Dossier>();
    public DbSet<SignTask> SignTasks => Set<SignTask>();
    public DbSet<SignSlotDef> SignSlotDefs => Set<SignSlotDef>();
    public DbSet<UserSignature> UserSignatures => Set<UserSignature>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<DigitalIdentity> DigitalIdentities => Set<DigitalIdentity>();
    public DbSet<SignEvent> SignEvents => Set<SignEvent>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
    public DbSet<DossierContent> DossierContents => Set<DossierContent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // USER
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasOne(x => x.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(x => x.DepartmentId);
        });

        // ROLE
        b.Entity<Role>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
        });

        // USERROLE
        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        // DEPARTMENT
        b.Entity<Department>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        });

        // DOSSIER
        b.Entity<Dossier>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SIGNTASK  (gộp tất cả cấu hình tại đây)
        b.Entity<SignTask>(e =>
        {
            e.HasIndex(x => new { x.DossierId, x.Order }).IsUnique();
            e.HasIndex(x => new { x.DossierId, x.SlotKey, x.Status, x.IsActivated });

            e.HasOne(x => x.Dossier)
                .WithMany(d => d.SignTasks)
                .HasForeignKey(x => x.DossierId);

            e.HasOne(x => x.Assignee)
                .WithMany()
                .HasForeignKey(x => x.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.VisiblePattern).HasMaxLength(64);
        });

        // USERSIGNATURE
        b.Entity<UserSignature>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.ContentType).HasMaxLength(64);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SIGNSLOTDEF
        b.Entity<SignSlotDef>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique(); // mỗi slot key chỉ 1 dòng
            e.Property(x => x.OrderInPhase).IsRequired();
            e.Property(x => x.Optional).HasDefaultValue(false);
        });

        // SYSTEMCONFIG
        b.Entity<SystemConfig>(e =>
        {
            e.HasIndex(x => x.Slot).IsUnique();
        });

        // DIGITALIDENTITY
        b.Entity<DigitalIdentity>(e =>
        {
            e.Property(x => x.EmpCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.CertName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Company).HasMaxLength(128).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.Title).HasMaxLength(128);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAtUtc).IsRequired();

            // NEW (GĐ5.1)
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.SerialNo).HasMaxLength(128);
            e.Property(x => x.Issuer).HasMaxLength(256);
            e.Property(x => x.Subject).HasMaxLength(256);
            e.Property(x => x.NotBefore);
            e.Property(x => x.NotAfter);

            e.HasIndex(x => new { x.UserId, x.IsActive });
        });

        // SIGNEVENT
        b.Entity<SignEvent>(e =>
        {
            e.Property(x => x.PdfPathIn).HasMaxLength(1024).IsRequired();
            e.Property(x => x.PdfPathOut).HasMaxLength(1024).IsRequired();
            e.Property(x => x.SearchPattern).HasMaxLength(256);
            e.Property(x => x.VisibleSignatureName).HasMaxLength(128);
            e.Property(x => x.CreatedAtUtc).IsRequired();
            e.Property(x => x.Error).HasMaxLength(1000);

            e.HasIndex(x => new { x.DossierId, x.CreatedAtUtc });
            // thêm index phục vụ lọc nhanh theo các màn hình audit
            e.HasIndex(x => new { x.DossierId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
        });

        // TEMPLATE
        b.Entity<Template>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TemplateVersion>(e =>
        {
            e.HasIndex(x => new { x.TemplateId, x.VersionNo }).IsUnique();
            e.Property(x => x.FileNameDocx).HasMaxLength(256).IsRequired();
            e.Property(x => x.FileNamePdf).HasMaxLength(256).IsRequired();
            e.Property(x => x.VisiblePatternsJson).HasMaxLength(4000).IsRequired();

            e.HasOne(x => x.Template)
                .WithMany(t => t.Versions)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        //DOSSIER CONTENT
        b.Entity<DossierContent>(e =>
        {
            e.HasIndex(x => x.DossierId).IsUnique();                  // 1 dossier ↔ 1 content
            e.Property(x => x.TemplateCode).HasMaxLength(64);
            e.Property(x => x.DataJson).HasMaxLength(8000).IsRequired();

            e.HasOne(x => x.Dossier)
                .WithOne()                                            // không vòng lặp navigation
                .HasForeignKey<DossierContent>(x => x.DossierId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.UpdatedBy)
                .WithMany()
                .HasForeignKey(x => x.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
