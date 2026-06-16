using BackEnd.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackEnd.Data.Configurations;

public class SeatAssignmentConfiguration : IEntityTypeConfiguration<SeatAssignment>
{
    public void Configure(EntityTypeBuilder<SeatAssignment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.SeatId).IsUnique();
        builder.HasIndex(a => a.EmployeeId).IsUnique();
        builder.Property(a => a.AssignedAt).HasColumnType("timestamptz");

        builder.HasOne(a => a.Employee)
               .WithOne(e => e.SeatAssignment)
               .HasForeignKey<SeatAssignment>(a => a.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
