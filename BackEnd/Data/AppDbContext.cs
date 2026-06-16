using BackEnd.Data.Configurations;
using BackEnd.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<FloorMap> FloorMaps => Set<FloorMap>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatAssignment> SeatAssignments => Set<SeatAssignment>();
    public DbSet<AttendanceState> AttendanceStates => Set<AttendanceState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.ApplyConfiguration(new FloorConfiguration());
        modelBuilder.ApplyConfiguration(new FloorMapConfiguration());
        modelBuilder.ApplyConfiguration(new SeatConfiguration());
        modelBuilder.ApplyConfiguration(new SeatAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        modelBuilder.ApplyConfiguration(new AttendanceStateConfiguration());
    }
}
