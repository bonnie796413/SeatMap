using BackEnd.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackEnd.Data.Configurations;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).HasMaxLength(100).IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnType("timestamptz");

        builder.HasMany(f => f.Seats)
               .WithOne(s => s.Floor)
               .HasForeignKey(s => s.FloorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.FloorMap)
               .WithOne(m => m.Floor)
               .HasForeignKey<FloorMap>(m => m.FloorId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
