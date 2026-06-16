using BackEnd.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackEnd.Data.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SeatNumber).HasMaxLength(20).IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnType("timestamptz");
        builder.Property(s => s.Location).HasColumnType("geometry(Point)");
        builder.HasIndex(s => new { s.FloorId, s.SeatNumber }).IsUnique();

        builder.HasOne(s => s.SeatAssignment)
               .WithOne(a => a.Seat)
               .HasForeignKey<SeatAssignment>(a => a.SeatId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
