using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Recam.Models.Entities;

namespace Recam.DataAccess;

public class RecamDbContext : IdentityDbContext<ApplicationUser>
{
    public RecamDbContext(DbContextOptions<RecamDbContext> options) : base(options)
    {
    }
    
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<PhotographyCompany> PhotographyCompanies => Set<PhotographyCompany>();
    public DbSet<ListingCase> ListingCases => Set<ListingCase>();
    public DbSet<CaseContact> CaseContacts => Set<CaseContact>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<AgentListingCase> AgentListingCases => Set<AgentListingCase>();
    public DbSet<AgentPhotographyCompany> AgentPhotographyCompanies => Set<AgentPhotographyCompany>();
    public DbSet<SelectedMedia> SelectedMedia { get; set; }
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // agent (PK=Id=UserId)
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AgentFirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AgentLastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.HasOne(x => x.User).WithOne().HasForeignKey<Agent>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        
        // PhotographyCompany (PK=Id=UserId)
        modelBuilder.Entity<PhotographyCompany>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PhotographyCompanyName).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.User).WithOne().HasForeignKey<PhotographyCompany>(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        });
        
        // ListingCase
        modelBuilder.Entity<ListingCase>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(2000);
            
            entity.Property(x => x.Street).HasMaxLength(200).IsRequired();
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.State).HasMaxLength(100).IsRequired();
            
            entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
            entity.Property(x => x.FloorArea).HasColumnType("decimal(12,2)");
            entity.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            
            //enum to int ^_^
            entity.Property(x => x.PropertyType).HasConversion<int>();
            entity.Property(x => x.SaleCategory).HasConversion<int>();
            entity.Property(x => x.ListingCaseStatus).HasConversion<int>();

            entity.Property(x => x.CoverImageUrl).HasMaxLength(2048);
            
            // CreatedBy
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);  // use userid to show all list
            
            // 1 to many
            entity.HasMany(x => x.Contacts).WithOne(m => m.ListingCase).HasForeignKey(x => x.ListingCaseId).OnDelete(DeleteBehavior.Cascade);
            
            entity.HasMany(x => x.Media).WithOne(m => m.ListingCase).HasForeignKey(x => x.ListingCaseId).OnDelete(DeleteBehavior.Cascade);
        });
        
        // CaseContact
        modelBuilder.Entity<CaseContact>(entity =>
        {
            entity.HasKey(x => x.ContactId);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.ProfileUrl).HasMaxLength(500);
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(50).IsRequired();
        });
        
        //MediaAsset
        modelBuilder.Entity<MediaAsset>(entity =>
            {
                entity.Property(x => x.MediaUrl).HasMaxLength(500).IsRequired();
                entity.Property(x => x.MediaType).HasConversion<int>();
                entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            }
        );
        
        // M2M: Agent <-> ListingCase
        modelBuilder.Entity<AgentListingCase>(entity =>
        {
            entity.HasKey(x => new { x.AgentId, x.ListingCaseId });   // avoid repeat combination
            entity.HasOne(x => x.Agent).WithMany(a => a.AgentListingCases).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ListingCase).WithMany(l => l.AgentListingCases).HasForeignKey(x => x.ListingCaseId).OnDelete(DeleteBehavior.Cascade);
        });
        
        // M2M Agent <-> PhotographyCompany
        modelBuilder.Entity<AgentPhotographyCompany>(entity =>
        {
            entity.HasKey(x => new { x.AgentId, x.PhotographyCompanyId });
            entity.HasOne(x => x.Agent).WithMany(a => a.AgentPhotographyCompanies).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PhotographyCompany).WithMany(p => p.AgentPhotographyCompanies).HasForeignKey(x => x.PhotographyCompanyId).OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<CaseHistory>(e =>
        {
            e.ToTable("CaseHistories");
            e.Property(x => x.Event).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActorUserId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.ListingCaseId);
            e.HasIndex(x => x.AtUtc);
        });
        
        modelBuilder.Entity<SelectedMedia>(b =>
        {
            b.HasKey(x => x.Id);

            b.HasOne(x => x.ListingCase)
                .WithMany() // 如果你后来加了导航，比如 ListingCase.SelectedMedia，就填 .WithMany(l => l.SelectedMedia)
                .HasForeignKey(x => x.ListingCaseId)
                .OnDelete(DeleteBehavior.NoAction); // 不要 cascade

            b.HasOne(x => x.MediaAsset)
                .WithMany() // 同上，如果 MediaAsset 有 SelectedMedia 集合就填上
                .HasForeignKey(x => x.MediaAssetId)
                .OnDelete(DeleteBehavior.NoAction); // 不要 cascade

            b.HasOne(x => x.Agent)
                .WithMany() // 如果 Agent 有集合就用那个导航
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.NoAction); // 不要 cascade
            // 允许 AgentId 为 null，不需要额外 .IsRequired(false)，因为属性本身已是 string?
        });
        
        
    }
    
}