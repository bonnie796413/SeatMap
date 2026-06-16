using BackEnd.Data;
using BackEnd.Dtos.Attendance;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Services;

public class AttendanceService(AppDbContext db)
{
    public async Task<AttendanceStatusResponse> CheckInAsync(Guid employeeId)
    {
        var state = await GetOrCreateStateAsync(employeeId);
        state.IsPresent = true;
        state.LastCheckInAt = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(state);
    }

    public async Task<AttendanceStatusResponse> CheckOutAsync(Guid employeeId)
    {
        var state = await GetOrCreateStateAsync(employeeId);
        state.IsPresent = false;
        state.LastCheckOutAt = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(state);
    }

    public async Task<AttendanceStatusResponse> GetStatusAsync(Guid employeeId)
    {
        var state = await db.AttendanceStates
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId)
            ?? throw new NotFoundException($"找不到員工 {employeeId} 的打卡狀態");
        return ToResponse(state);
    }

    private async Task<AttendanceState> GetOrCreateStateAsync(Guid employeeId)
    {
        var state = await db.AttendanceStates
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId);

        if (state is null)
        {
            if (!await db.Employees.AnyAsync(e => e.Id == employeeId))
                throw new NotFoundException($"找不到員工 {employeeId}");

            state = new AttendanceState
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                IsPresent = false,
                UpdatedAt = DateTime.UtcNow
            };
            db.AttendanceStates.Add(state);
        }
        return state;
    }

    private static AttendanceStatusResponse ToResponse(AttendanceState s) =>
        new(s.EmployeeId, s.IsPresent, s.LastCheckInAt, s.LastCheckOutAt, s.UpdatedAt);
}
