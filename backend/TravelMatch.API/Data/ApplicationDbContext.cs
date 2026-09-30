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
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TripRequest>(entity =>
{
    entity.HasKey(t => t.Id);

    entity.Property(t => t.Destination)
        .IsRequired()
        .HasMaxLength(200);

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

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(p => p.EstimatedExpenses)
                .HasPrecision(18, 2);

            entity.Property(p => p.Availability)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(p => p.Inclusions)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(p => p.Exclusions)
                .HasMaxLength(4000);

            entity.Property(p => p.AdditionalNotes)
                .HasMaxLength(2000);

            entity.Property(p => p.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(p => p.SubmittedAt)
                .IsRequired();

            entity.HasIndex(p => new { p.GuideId, p.TripRequestId })
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending'");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.GuideId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<TripRequest>()
                .WithMany()
                .HasForeignKey(p => p.TripRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(notification => notification.Id);

            entity.Property(notification => notification.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(notification => notification.Message)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(notification => notification.IsRead)
                .IsRequired();

            entity.Property(notification => notification.CreatedAt)
                .IsRequired();

            entity.HasIndex(notification => new
            {
                notification.RecipientUserId,
                notification.CreatedAt
            });

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(notification => notification.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<TripRequest>()
                .WithMany()
                .HasForeignKey(notification => notification.TripRequestId)
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