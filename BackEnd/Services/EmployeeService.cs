using BackEnd.Data;
using BackEnd.Dtos.Employees;
using BackEnd.Entities;
using BackEnd.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Services;

public class EmployeeService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager)
{
    public async Task<List<EmployeeResponse>> GetAllAsync()
    {
        var employees = await db.Employees
            .Include(e => e.AttendanceState)
            .Include(e => e.SeatAssignment)
                .ThenInclude(a => a!.Seat)
            .ToListAsync();

        var users = await userManager.Users
            .Where(u => u.EmployeeId != null)
            .ToListAsync();

        return employees.Select(e =>
        {
            var user = users.FirstOrDefault(u => u.EmployeeId == e.Id);
            return ToResponse(e, user?.UserName);
        }).ToList();
    }

    public async Task<EmployeeResponse> GetByIdAsync(Guid id)
    {
        var employee = await db.Employees
            .Include(e => e.AttendanceState)
            .Include(e => e.SeatAssignment)
                .ThenInclude(a => a!.Seat)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"找不到員工 {id}");

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);
        return ToResponse(employee, user?.UserName);
    }

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName)) throw new ValidationException("姓名不可為空");
        if (string.IsNullOrWhiteSpace(req.Username)) throw new ValidationException("帳號不可為空");
        if (string.IsNullOrWhiteSpace(req.Password)) throw new ValidationException("密碼不可為空");
        if (req.Password.Length < 8) throw new ValidationException("密碼長度至少 8 字元");
        if (!req.Password.Any(char.IsLetter) || !req.Password.Any(char.IsDigit))
            throw new ValidationException("密碼須包含英文字母與數字");

        if (await userManager.FindByNameAsync(req.Username) is not null)
            throw new ConflictException($"帳號 {req.Username} 已被使用");

        await using var tx = await db.Database.BeginTransactionAsync();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = req.FullName,
            Department = req.Department,
            AvatarUrl = req.AvatarUrl,
            CreatedAt = DateTime.UtcNow
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = req.Username,
            Email = $"{req.Username}@seatmap.local",
            EmailConfirmed = true,
            EmployeeId = employee.Id,
            CreatedAt = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            await tx.RollbackAsync();
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new ValidationException($"建立帳號失敗：{errors}");
        }

        await userManager.AddToRoleAsync(user, "Employee");

        db.AttendanceStates.Add(new AttendanceState
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            IsPresent = false,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ToResponse(employee, req.Username);
    }

    public async Task<EmployeeResponse> UpdateAsync(Guid id, UpdateEmployeeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName)) throw new ValidationException("姓名不可為空");

        var employee = await db.Employees
            .Include(e => e.AttendanceState)
            .Include(e => e.SeatAssignment)
                .ThenInclude(a => a!.Seat)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"找不到員工 {id}");

        employee.FullName = req.FullName;
        employee.Department = req.Department;
        employee.AvatarUrl = req.AvatarUrl;
        await db.SaveChangesAsync();

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);
        return ToResponse(employee, user?.UserName);
    }

    public async Task DeleteAsync(Guid id)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"找不到員工 {id}");

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.EmployeeId == id);
        if (user is not null)
            await userManager.DeleteAsync(user);

        db.Employees.Remove(employee);
        await db.SaveChangesAsync();
    }

    public async Task<List<EmployeeSearchResult>> SearchAsync(string name)
    {
        return await db.Employees
            .Where(e => EF.Functions.ILike(e.FullName, $"%{name}%"))
            .Include(e => e.AttendanceState)
            .Include(e => e.SeatAssignment)
                .ThenInclude(a => a!.Seat)
            .Take(20)
            .Select(e => new EmployeeSearchResult(
                e.Id,
                e.FullName,
                e.Department,
                e.AvatarUrl,
                e.AttendanceState != null && e.AttendanceState.IsPresent,
                e.SeatAssignment == null ? null : new SeatSummary(
                    e.SeatAssignment.SeatId,
                    e.SeatAssignment.Seat.FloorId,
                    e.SeatAssignment.Seat.SeatNumber,
                    e.SeatAssignment.Seat.Location.X,
                    e.SeatAssignment.Seat.Location.Y)))
            .ToListAsync();
    }

    private static EmployeeResponse ToResponse(Employee e, string? username) =>
        new(e.Id, e.FullName, e.Department, e.AvatarUrl, username,
            e.AttendanceState?.IsPresent ?? false,
            e.SeatAssignment == null ? null : new SeatSummary(
                e.SeatAssignment.SeatId,
                e.SeatAssignment.Seat.FloorId,
                e.SeatAssignment.Seat.SeatNumber,
                e.SeatAssignment.Seat.Location.X,
                e.SeatAssignment.Seat.Location.Y));
}
