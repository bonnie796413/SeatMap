using System.Threading.Channels;

namespace BackEnd.Services;

public class TileConversionChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelWriter<Guid> Writer => _channel.Writer;
    public ChannelReader<Guid> Reader => _channel.Reader;
}
