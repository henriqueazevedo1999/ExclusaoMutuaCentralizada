using System.Net;
using System.Net.Sockets;

namespace ExclusaoMutua;

public class Conexao
{
    public const string PERMITIR_ACESSO = "PERMITIR";
    public const string NEGAR_ACESSO = "NAO_PERMITIR";
    private const int PORTA = 8000;
    
    private bool _conectado = true;
    private Socket _sock;
    private TcpListener _listenSocket;

    ~Conexao()
    {
        _sock?.Close();
        _sock?.Dispose();
    }

    public void Conectar(Processo coordenador)
    {
        Console.WriteLine($"Coordenador {coordenador} pronto para receber requisicoes.");
        new Thread(() =>
        {
            try
            {
                // cria um socket TCP para pedidos de conexão
                _listenSocket = new TcpListener(IPAddress.Any, PORTA);
                _listenSocket.Start();

                // fica conectado enquanto o coordenador estiver vivo
                while (_conectado)
                {
                    // aguarda ate um cliente pedir por uma conexao
                    _sock = _listenSocket.AcceptSocket();

                    // prepara um buffer para receber dados do cliente
                    using (StreamReader s = new(new NetworkStream(_sock)))
                        Console.WriteLine(s.ReadLine());

                    // coloca a resposta em um buffer e envia para o cliente
                    using StreamWriter d = new(new NetworkStream(_sock));
                    string sBuf = $"{(coordenador.IsRecursoEmUso() ? NEGAR_ACESSO : PERMITIR_ACESSO)}\n"; 

                    d.Write(sBuf);
                    d.Flush();
                }
            }
            catch (IOException) { }

            Console.WriteLine("Conexao encerrada.");
        }).Start();
    }

    public static string RealizarRequisicao(string mensagem)
    {
        try
        {
            // cria um socket TCP para conexao com localhost:PORTA
            using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Connect("localhost", PORTA);

            // coloca os dados em um buffer e envia para o servidor
            using (var d = new StreamWriter(new NetworkStream(sock)))
            {
                d.Write(mensagem);
                d.Flush();
            }

            // prepara um buffer para receber a resposta do servidor
            using var s = new StreamReader(new NetworkStream(sock));

            // le os dados enviados pela aplicacao servidora
            return s.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("A requisicao nao foi finalizada corretamente.");
            Console.WriteLine(ex.Message);
            return "ERROR!";
        }
    }

    public void EncerrarConexao()
    {
        _conectado = false;
        try
        {
            _sock?.Close();
            _listenSocket?.Stop();
        }
        catch (Exception ex)
        {
            if (ex is not IOException and not NullReferenceException)
                throw;

            Console.WriteLine("Erro ao encerrar a conexao: ");
            Console.WriteLine(ex.Message);
        }
    }
}