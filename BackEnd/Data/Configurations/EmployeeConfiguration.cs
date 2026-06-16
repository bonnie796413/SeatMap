using BackEnd.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackEnd.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FullName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Department).HasMaxLength(100);
        builder.Property(e => e.AvatarUrl).HasMaxLength(2048);
        builder.Property(e => e.CreatedAt).HasColumnType("timestamptz");

        builder.HasOne(e => e.AttendanceState)
               .WithOne(a => a.Employee)
               .HasForeignKey<AttendanceState>(a => a.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
