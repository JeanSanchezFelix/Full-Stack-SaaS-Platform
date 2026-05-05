using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customer");
            entity.HasKey(customer => customer.Id);

            entity.Property(customer => customer.Id).HasColumnName("id");
            entity.Property(customer => customer.CustomerCode).HasColumnName("customer_code");
            entity.Property(customer => customer.FirstName).HasColumnName("first_name");
            entity.Property(customer => customer.LastName).HasColumnName("last_name");
            entity.Property(customer => customer.LicenseNumber).HasColumnName("license_number");
            entity.Property(customer => customer.PhoneNumber).HasColumnName("phone_number");
            entity.Property(customer => customer.Email).HasColumnName("email");
            entity.Property(customer => customer.City).HasColumnName("city");
            entity.Property(customer => customer.Country).HasColumnName("country");
            entity.Property(customer => customer.LiabilityWaiverSigned).HasColumnName("liability_waiver_signed");
            entity.Property(customer => customer.LiabilityWaiverSignedAt).HasColumnName("liability_waiver_signed_at");
            entity.Property(customer => customer.ElectronicSignature).HasColumnName("electronic_signature");
            entity.Property(customer => customer.AuthorizeRecontact).HasColumnName("authorize_recontact");
            entity.Property(customer => customer.CreatedAt).HasColumnName("created_at");
            entity.Property(customer => customer.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(customer => customer.CustomerCode).IsUnique();
            entity.HasIndex(customer => customer.LicenseNumber).IsUnique();
            entity.HasIndex(customer => customer.Email).IsUnique();
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("booking");
            entity.HasKey(booking => booking.Id);

            entity.Property(booking => booking.Id).HasColumnName("id");
            entity.Property(booking => booking.CustomerId).HasColumnName("customer_id");
            entity.Property(booking => booking.RequestedStart).HasColumnName("requested_start");
            entity.Property(booking => booking.RequestedEnd).HasColumnName("requested_end");
            entity.Property(booking => booking.ScooterQuantity).HasColumnName("scooter_quantity");
            entity.Property(booking => booking.EbikeQuantity).HasColumnName("ebike_quantity");
            entity.Property(booking => booking.Status).HasColumnName("status");
            entity.Property(booking => booking.AdminNotes).HasColumnName("admin_notes");
            entity.Property(booking => booking.ApprovedBy).HasColumnName("approved_by");
            entity.Property(booking => booking.ApprovedAt).HasColumnName("approved_at");
            entity.Property(booking => booking.EstimatedTotal).HasColumnName("estimated_total").HasPrecision(10, 2);
            entity.Property(booking => booking.CreatedAt).HasColumnName("created_at");
            entity.Property(booking => booking.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(booking => booking.Customer)
                .WithMany(customer => customer.Bookings)
                .HasForeignKey(booking => booking.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Rental>(entity =>
        {
            entity.ToTable("rental");
            entity.HasKey(rental => rental.Id);

            entity.Property(rental => rental.Id).HasColumnName("id");
            entity.Property(rental => rental.BookingId).HasColumnName("booking_id");
            entity.Property(rental => rental.CustomerId).HasColumnName("customer_id");
            entity.Property(rental => rental.StartTime).HasColumnName("start_time");
            entity.Property(rental => rental.EndTime).HasColumnName("end_time");
            entity.Property(rental => rental.ScooterQuantity).HasColumnName("scooter_quantity");
            entity.Property(rental => rental.EbikeQuantity).HasColumnName("ebike_quantity");
            entity.Property(rental => rental.TotalPrice).HasColumnName("total_price").HasPrecision(10, 2);
            entity.Property(rental => rental.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(rental => rental.BookingId).IsUnique();

            entity.HasOne(rental => rental.Customer)
                .WithMany(customer => customer.Rentals)
                .HasForeignKey(rental => rental.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rental => rental.Booking)
                .WithOne(booking => booking.Rental)
                .HasForeignKey<Rental>(rental => rental.BookingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("review");
            entity.HasKey(review => review.Id);

            entity.Property(review => review.Id).HasColumnName("id");
            entity.Property(review => review.CustomerId).HasColumnName("customer_id");
            entity.Property(review => review.RentalId).HasColumnName("rental_id");
            entity.Property(review => review.Rating).HasColumnName("rating");
            entity.Property(review => review.Comment).HasColumnName("comment");
            entity.Property(review => review.CreatedAt).HasColumnName("created_at");

            entity.HasOne(review => review.Customer)
                .WithMany(customer => customer.Reviews)
                .HasForeignKey(review => review.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(review => review.Rental)
                .WithMany(rental => rental.Reviews)
                .HasForeignKey(review => review.RentalId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
