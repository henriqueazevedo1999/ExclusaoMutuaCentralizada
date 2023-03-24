using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ExclusaoMutua;

public class Processo : IDisposable
{
    private const int USO_PROCESSO_MIN = 10000;
    private const int USO_PROCESSO_MAX = 15000;

    protected const int PORTA = 8000;

    private bool _disposed;
    private Coordenador _coordenador;

    public Processo(int pid)
    {
        Pid = pid;
    }

    public bool EhCoordenador => _coordenador != null;
    public int Pid { get; init; }

    private void PromoverACoordenador()
    {
        if (EhCoordenador)
            return;

        _coordenador = new Coordenador(this);
    }

    public async Task AcessarRecursoCompartilhado()
    {
        try
        {
            Mensagem mensagem = new(Pid, TipoMensagem.Requisicao);

            try
            {
                using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //sock.SendTimeout = 3000;
                sock.ReceiveTimeout = 1000;

                sock.Connect("localhost", PORTA);
                await ConexaoHelper.SendMessageAsync(sock, mensagem.ToString());

                using var s = new StreamReader(new NetworkStream(sock));
                while (true)
                {
                    Thread.Sleep(10);
                    string resposta = s.ReadLine();
                    if (resposta == null) // conexão foi fechada, ou seja, coordenador morreu
                    {
                        await AcessarRecursoCompartilhado();
                        return;
                    }

                    Mensagem responseMessage = JsonSerializer.Deserialize<Mensagem>(resposta);

                    if (responseMessage.Tipo == TipoMensagem.Negacao)
                        sock.ReceiveTimeout = 0;
                    else if (responseMessage.Tipo == TipoMensagem.Concessao)
                        break;
                }
            }
            catch (Exception ex)
            {
                if (ex is not IOException and not SocketException)
                    throw;

                PromoverACoordenador();
                await AcessarRecursoCompartilhado();
                return;
            }

            UtilizarRecurso();

            await LiberarRecurso();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Processo {Pid} - Erro ao acessar recurso compartilhado: {ex}");
            throw;
        }
    }

    private void UtilizarRecurso()
    {
        Random random = new();
        int randomUsageTime = USO_PROCESSO_MIN + random.Next(USO_PROCESSO_MAX - USO_PROCESSO_MIN);

        Console.WriteLine($"Processo {this} está consumindo o recurso.");

        Thread.Sleep(randomUsageTime);

        Console.WriteLine($"Processo {this} parou de consumir o recurso.");
    }

    private async Task LiberarRecurso()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect("localhost", PORTA);
            socket.SendTimeout = 1000;

            Mensagem mensagem = new(Pid, TipoMensagem.Liberacao);
            await ConexaoHelper.SendMessageAsync(socket, mensagem.ToString());
        }
        catch (Exception ex)
        {
            if (ex is not IOException and not SocketException)
                throw;
        }
    }

    public override string ToString()
    {
        return Pid.ToString();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _coordenador?.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private class Coordenador : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<ComunicacaoProcesso> _filaEspera = new();
        private readonly Processo _processo;

        private bool _recursoEmUso = false;
        private bool _disposed;

        public Coordenador(Processo processo)
        {
            _processo = processo;
            Conectar(_cts.Token);
        }

        private void Conectar(CancellationToken cancellationToken)
        {
            new Thread(async () =>
            {
                TcpListener listenSocket = null;
                try
                {
                    // cria um socket TCP para pedidos de conexão
                    listenSocket = new(IPAddress.Any, PORTA);
                    listenSocket.Start();

                    ConsoleLog($"Pronto para receber requisicoes.");
                    ProcessaFila(cancellationToken);

                    // fica conectado enquanto o coordenador estiver vivo
                    while (true)
                    {
                        Thread.Sleep(1);
                        cancellationToken.ThrowIfCancellationRequested();

                        // aguarda ate um cliente pedir por uma conexao
                        Socket socket = await listenSocket.AcceptSocketAsync();
                        socket.ReceiveTimeout = 1000;

                        await Task.Run(async () =>
                        {
                            try
                            {
                                Mensagem mensagemEntrada;
                                // prepara um buffer para receber dados do cliente
                                using (StreamReader streamEntrada = new(new NetworkStream(socket)))
                                {
                                    string message = streamEntrada.ReadLine();
                                    mensagemEntrada = JsonSerializer.Deserialize<Mensagem>(message);
                                }

                                cancellationToken.ThrowIfCancellationRequested();

                                switch (mensagemEntrada.Tipo)
                                {
                                    case TipoMensagem.Requisicao:
                                    {
                                        ConsoleLog($"Processo {mensagemEntrada.Pid} solicitando acesso ao recurso");

                                        if (_recursoEmUso)
                                        {
                                            Mensagem mensagemSaida = mensagemEntrada with { Tipo = TipoMensagem.Negacao };
                                            _filaEspera.Enqueue(new ComunicacaoProcesso(mensagemEntrada.Pid, socket));

                                            cancellationToken.ThrowIfCancellationRequested();

                                            ConsoleLog($"Adicionando processo {mensagemEntrada.Pid} na lista de espera: {string.Join(",", _filaEspera.Select(x => x.Pid))}");

                                            await ConexaoHelper.SendMessageAsync(socket, mensagemSaida.ToString());
                                        }
                                        else
                                        {
                                            _filaEspera.Enqueue(new ComunicacaoProcesso(mensagemEntrada.Pid, socket));
                                        }
                                    }
                                    break;

                                    case TipoMensagem.Liberacao:
                                    {
                                        ConsoleLog($"Processo {mensagemEntrada.Pid} informou liberação de recurso");
                                        _recursoEmUso = false;
                                    }
                                    break;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                socket.Close();
                                throw;
                            }
                            catch (Exception ex)
                            {
                                socket.Close();
                                ConsoleLog($"Erro na thread de conexão: {ex}");
                                throw;
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (SocketException) // já existe outro coordenador
                {
                    Dispose();
                }
                catch (Exception ex)
                {
                    ConsoleLog($"Erro no socket: {ex}");
                    throw;
                }
                finally
                {
                    listenSocket.Stop();
                }
            }).Start();
        }

        private void ProcessaFila(CancellationToken cancellationToken)
        {
            new Thread(async () =>
            {
                try
                {
                    //falta try catch?
                    ConsoleLog($"Iniciando processamento da fila.");

                    while (true)
                    {
                        Thread.Sleep(10);

                        cancellationToken.ThrowIfCancellationRequested();

                        if (_recursoEmUso)
                            continue;

                        if (_filaEspera.IsEmpty)
                            continue;

                        _recursoEmUso = true;
                        if (!_filaEspera.TryDequeue(out ComunicacaoProcesso item))
                        {
                            ConsoleLog("Falha ao pegar item da fila");
                            continue;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        ConsoleLog($"Liberando acesso ao recurso para processo {item.Pid}");

                        Mensagem mensagem = new(item.Pid, TipoMensagem.Concessao);
                        await ConexaoHelper.SendMessageAsync(item.Socket, mensagem.ToString());
                        item.Socket.Close();
                    };
                }
                catch (OperationCanceledException) { }
            }).Start();
        }

        private void ConsoleLog(string message)
        {
            Console.WriteLine($"Coordenador {_processo.Pid}: {message}");
        }

        private record ComunicacaoProcesso
        {
            public ComunicacaoProcesso(int pid, Socket socket)
            {
                Pid = pid;
                Socket = socket;
            }

            public int Pid { get; set; }
            public Socket Socket { get; set; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cts.Cancel();
                    _cts.Dispose();

                    while (_filaEspera.TryDequeue(out ComunicacaoProcesso processo))
                        processo.Socket.Close();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
