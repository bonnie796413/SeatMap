namespace BackEnd.Entities;

public class Floor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public FloorMap? FloorMap { get; set; }
    public ICollection<Seat> Seats { get; set; } = [];
}
