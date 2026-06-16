using BackEnd.Data;
using BackEnd.Dtos.Assignments;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Services;

public class AssignmentService(AppDbContext db)
{
    public async Task<AssignmentResponse> AssignAsync(Guid seatId, Guid employeeId)
    {
        var seat = await db.Seats.Include(s => s.Floor)
            .FirstOrDefaultAsync(s => s.Id == seatId)
            ?? throw new NotFoundException($"找不到座位 {seatId}");

        if (!await db.Employees.AnyAsync(e => e.Id == employeeId))
            throw new NotFoundException($"找不到員工 {employeeId}");

        await using var tx = await db.Database.BeginTransactionAsync();

        // 座位已被他人占用
        var existingBySeat = await db.SeatAssignments
            .FirstOrDefaultAsync(a => a.SeatId == seatId);
        if (existingBySeat != null && existingBySeat.EmployeeId != employeeId)
            throw new ConflictException("此座位已被其他員工占用");

        // 冪等：已指派同一座位
        if (existingBySeat?.EmployeeId == employeeId)
        {
            await tx.CommitAsync();
            return new AssignmentResponse(
                seatId, seat.SeatNumber, seat.FloorId,
                employeeId,
                (await db.Employees.FindAsync(employeeId))!.FullName,
                existingBySeat.AssignedAt);
        }

        // 移除員工舊有指派
        var existingByEmployee = await db.SeatAssignments
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId);
        if (existingByEmployee != null)
            db.SeatAssignments.Remove(existingByEmployee);

        var assignment = new SeatAssignment
        {
            Id = Guid.NewGuid(),
            SeatId = seatId,
            EmployeeId = employeeId,
            AssignedAt = DateTime.UtcNow
        };
        db.SeatAssignments.Add(assignment);

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            throw new ConflictException("座位指派衝突，請重試");
        }

        var emp = await db.Employees.FindAsync(employeeId);
        return new AssignmentResponse(
            seatId, seat.SeatNumber, seat.FloorId,
            employeeId, emp!.FullName, assignment.AssignedAt);
    }

    public async Task UnassignBySeatAsync(Guid seatId)
    {
        var assignment = await db.SeatAssignments
            .FirstOrDefaultAsync(a => a.SeatId == seatId)
            ?? throw new NotFoundException($"座位 {seatId} 尚未指派");
        db.SeatAssignments.Remove(assignment);
        await db.SaveChangesAsync();
    }

    public async Task UnassignByEmployeeAsync(Guid employeeId)
    {
        var assignment = await db.SeatAssignments
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId)
            ?? throw new NotFoundException($"員工 {employeeId} 尚未指派座位");
        db.SeatAssignments.Remove(assignment);
        await db.SaveChangesAsync();
    }
}
