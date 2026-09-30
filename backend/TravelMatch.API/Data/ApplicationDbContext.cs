using Microsoft.EntityFrameworkCore;
using TravelMatch.API.Models;

namespace TravelMatch.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<OrganizerProfile> OrganizerProfiles => Set<OrganizerProfile>();
    public DbSet<TouristProfile> TouristProfiles => Set<TouristProfile>();
    public DbSet<GuideProfile> GuideProfiles => Set<GuideProfile>();
    public DbSet<TripRequest> TripRequests => Set<TripRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TripRequest>(entity =>
{
    entity.HasKey(t => t.Id);

    entity.Property(t => t.Destination)
        .IsRequired()
        .HasMaxLength(200);

    entity.Property(t => t.TripType)
        .IsRequired()
        .HasMaxLength(80)
        .HasDefaultValue("Other");

    entity.Property(t => t.StartDate)
        .HasColumnType("date")
        .IsRequired();

    entity.Property(t => t.EndDate)
        .HasColumnType("date")
        .IsRequired();

    entity.Property(t => t.NumberOfTravelers)
        .IsRequired();

    entity.Property(t => t.Budget)
        .IsRequired()
        .HasPrecision(18, 2);

    entity.Property(t => t.Description)
        .HasMaxLength(2000);

    entity.Property(t => t.TravelPreferences)
        .IsRequired()
        .HasMaxLength(1000)
        .HasDefaultValue(string.Empty);

    entity.Property(t => t.Status)
        .HasConversion<string>()
        .IsRequired()
        .HasMaxLength(30);

    entity.Property(t => t.CreatedAt)
        .IsRequired();

    entity.HasOne<User>()
        .WithMany()
        .HasForeignKey(t => t.TouristId)
        .OnDelete(DeleteBehavior.Cascade);
});

        modelBuilder.Entity<GuideProfile>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.UserId)
                .IsRequired();

            entity.Property(p => p.Bio)
                .HasMaxLength(500);

            entity.Property(p => p.ServiceArea)
                
                .HasMaxLength(100);

            entity.Property(p => p.ExperienceYears)
                .IsRequired();
                
            entity.Property(p => p.AverageRating);

            

                entity.Property(p => p.VerificationStatus)
                      .HasConversion<string>()
                   .IsRequired()
                   .HasMaxLength(30);

       

            entity.HasOne<User>()
                  .WithOne()
                  .HasForeignKey<GuideProfile>(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);

            
        });





        modelBuilder.Entity<TouristProfile>(entity=>
        {
            entity.HasKey(p=>p.Id);

            entity.Property(p=>p.Preferences)
                .HasMaxLength(1000);
        
            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<TouristProfile>(p=>p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
         }
        
        
        );


        modelBuilder.Entity<OrganizerProfile>(entity=>{


            entity.HasKey(p=>p.Id);

            entity.Property(p=>p.GroupOrganizationName)
            .HasMaxLength(253);

            entity.Property(p=>p.Bio)
            .HasMaxLength(2000);

              entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<OrganizerProfile>(p=>p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
        });

















        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.Role)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(u => u.PhoneNumber)
                  .IsRequired()
                   .HasMaxLength(20);

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.IsActive)
                .IsRequired();
        });
    }
}