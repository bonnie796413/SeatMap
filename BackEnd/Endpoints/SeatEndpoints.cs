namespace BackEnd.Endpoints;

public static class SeatEndpoints
{
    public static void MapSeatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/seats").WithTags("Seats");

        group.MapGet("/", GetAllSeats)
            .WithName("GetAllSeats");

        group.MapGet("/{id}", GetSeatById)
            .WithName("GetSeatById");

        group.MapPost("/", CreateSeat)
            .WithName("CreateSeat");

        group.MapPut("/{id}", UpdateSeat)
            .WithName("UpdateSeat");

        group.MapDelete("/{id}", DeleteSeat)
            .WithName("DeleteSeat");
    }

    private static IResult GetAllSeats()
    {
        // TODO: 實作取得所有座位邏輯
        return Results.Ok(Array.Empty<object>());
    }

    private static IResult GetSeatById(int id)
    {
        // TODO: 實作取得單一座位邏輯
        return Results.NotFound();
    }

    private static IResult CreateSeat()
    {
        // TODO: 實作新增座位邏輯
        return Results.Created();
    }

    private static IResult UpdateSeat(int id)
    {
        // TODO: 實作更新座位邏輯
        return Results.NoContent();
    }

    private static IResult DeleteSeat(int id)
    {
        // TODO: 實作刪除座位邏輯
        return Results.NoContent();
    }
}
