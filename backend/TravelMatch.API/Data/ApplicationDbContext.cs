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
    public DbSet<OrganizedTrip> OrganizedTrips => Set<OrganizedTrip>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

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
        .HasMaxLength(100);

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

    entity.Property(t => t.BudgetMin)
        .IsRequired()
        .HasPrecision(18, 2);

    entity.Property(t => t.BudgetMax)
        .IsRequired()
        .HasPrecision(18, 2);

    entity.Property(t => t.Description)
        .HasMaxLength(2000);

    entity.Property(t => t.TravelPreferences)
        .IsRequired()
        .HasMaxLength(1000);

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

    entity.HasOne<User>()
        .WithMany()
        .HasForeignKey(t => t.MatchedGuideId)
        .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<OrganizedTrip>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Destination)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(t => t.StartDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(t => t.EndDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(t => t.MaxParticipants)
                .IsRequired();

            entity.Property(t => t.PricePerPerson)
                .IsRequired()
                .HasPrecision(18, 2);

            entity.Property(t => t.Inclusions)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(t => t.Exclusions)
                .HasMaxLength(2000);

            entity.Property(t => t.RegistrationDeadline)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(t => t.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(t => t.CreatedAt)
                .IsRequired();

            entity.Property(t => t.UpdatedAt);

            entity.HasOne<OrganizerProfile>()
                .WithMany()
                .HasForeignKey(t => t.OrganizerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.RegisteredAt)
                .IsRequired();

            entity.HasOne<OrganizedTrip>()
                .WithMany()
                .HasForeignKey(r => r.OrganizedTripId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<TouristProfile>()
                .WithMany()
                .HasForeignKey(r => r.TouristId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => r.OrganizedTripId);
            entity.HasIndex(r => new { r.OrganizedTripId, r.TouristId })
                .IsUnique();
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

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasOne(p => p.TripRequest)
                .WithMany()
                .HasForeignKey(p => p.TripRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Guide)
                .WithMany()
                .HasForeignKey(p => p.GuideId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Price)
                .IsRequired()
                .HasPrecision(18, 2);

            entity.Property(p => p.Availability)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(p => p.Inclusions)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(p => p.Exclusions)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(p => p.Notes)
                .HasMaxLength(2000);

            entity.Property(p => p.MatchingScore);

            entity.Property(p => p.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(p => p.CreatedAt)
                .IsRequired();

            entity.Property(p => p.UpdatedAt);

            entity.HasIndex(p => new { p.TripRequestId, p.GuideId })
                .IsUnique();
        });

        modelBuilder.Entity<ItineraryDay>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.HasOne(i => i.Proposal)
                .WithMany(p => p.ItineraryDays)
                .HasForeignKey(i => i.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(i => i.DayNumber)
                .IsRequired();

            entity.Property(i => i.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(i => i.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(i => i.Activities)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(i => i.Accommodation)
                .HasMaxLength(500);

            entity.Property(i => i.Meals)
                .HasMaxLength(500);

            entity.HasIndex(i => new { i.ProposalId, i.DayNumber })
                .IsUnique();
        });

        modelBuilder.Entity<GuideProfile>(entity =>
        {
            entity.Property(p => p.PhotoUrl)
                .HasMaxLength(500);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(n => n.CreatedAt)
                .IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(n => new { n.RecipientUserId, n.CreatedAt });
        });
    }
}
