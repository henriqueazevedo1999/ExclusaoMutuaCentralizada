using System.Net.Sockets;

namespace ExclusaoMutua;

public static class ConexaoHelper
{
    public static async Task SendMessageAsync(Socket socket, string message)
    {
        using StreamWriter stream = new(new NetworkStream(socket));
        await stream.WriteLineAsync(message);
        await stream.FlushAsync();
    }
}
