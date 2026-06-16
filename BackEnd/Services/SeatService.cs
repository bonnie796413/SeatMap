using BackEnd.Data;
using BackEnd.Dtos.Seats;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace BackEnd.Services;

public class SeatService(AppDbContext db)
{
    public async Task<List<SeatResponse>> GetByFloorAsync(Guid floorId)
    {
        return await db.Seats
            .Where(s => s.FloorId == floorId)
            .Include(s => s.SeatAssignment)
                .ThenInclude(a => a!.Employee)
                    .ThenInclude(e => e.AttendanceState)
            .Select(s => ToResponse(s))
            .ToListAsync();
    }

    public async Task<SeatResponse> GetByIdAsync(Guid id)
    {
        var seat = await db.Seats
            .Include(s => s.SeatAssignment)
                .ThenInclude(a => a!.Employee)
                    .ThenInclude(e => e.AttendanceState)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"找不到座位 {id}");

        return ToResponse(seat);
    }

    public async Task<SeatResponse> CreateAsync(CreateSeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SeatNumber))
            throw new ValidationException("座位編號不可為空");

        if (!await db.Floors.AnyAsync(f => f.Id == req.FloorId))
            throw new NotFoundException($"找不到樓層 {req.FloorId}");

        if (await db.Seats.AnyAsync(s => s.FloorId == req.FloorId && s.SeatNumber == req.SeatNumber))
            throw new ConflictException($"同樓層已有座位編號 {req.SeatNumber}");

        var seat = new Seat
        {
            Id = Guid.NewGuid(),
            FloorId = req.FloorId,
            SeatNumber = req.SeatNumber,
            Location = new Point(req.X, req.Y) { SRID = 0 },
            CreatedAt = DateTime.UtcNow
        };
        db.Seats.Add(seat);
        await db.SaveChangesAsync();
        return ToResponse(seat);
    }

    public async Task<SeatResponse> UpdateAsync(Guid id, UpdateSeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SeatNumber))
            throw new ValidationException("座位編號不可為空");

        var seat = await db.Seats
            .Include(s => s.SeatAssignment)
                .ThenInclude(a => a!.Employee)
                    .ThenInclude(e => e.AttendanceState)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"找不到座位 {id}");

        if (seat.SeatNumber != req.SeatNumber &&
            await db.Seats.AnyAsync(s => s.FloorId == seat.FloorId && s.SeatNumber == req.SeatNumber && s.Id != id))
            throw new ConflictException($"同樓層已有座位編號 {req.SeatNumber}");

        seat.SeatNumber = req.SeatNumber;
        seat.Location = new Point(req.X, req.Y) { SRID = 0 };
        await db.SaveChangesAsync();
        return ToResponse(seat);
    }

    public async Task DeleteAsync(Guid id)
    {
        var seat = await db.Seats.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new NotFoundException($"找不到座位 {id}");
        db.Seats.Remove(seat);
        await db.SaveChangesAsync();
    }

    private static SeatResponse ToResponse(Seat seat)
    {
        AssignmentInfo? assignment = null;
        bool? isPresent = null;

        if (seat.SeatAssignment?.Employee is { } emp)
        {
            assignment = new AssignmentInfo(
                emp.Id, emp.FullName, emp.AvatarUrl, emp.Department);
            isPresent = emp.AttendanceState?.IsPresent;
        }

        return new SeatResponse(
            seat.Id, seat.FloorId, seat.SeatNumber,
            seat.Location.X, seat.Location.Y,
            assignment, isPresent);
    }
}
