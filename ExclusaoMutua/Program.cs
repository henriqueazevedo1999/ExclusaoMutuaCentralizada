using System.Collections.Concurrent;

namespace ExclusaoMutua;

public static class Program
{
    //tempos ajustados para testes. falta ajustar de acordo com o trabalho
    private const double TEMPO_BASE = 0.3;
    private const int ADICIONA = (int)(15000 * TEMPO_BASE);
    private const int INATIVO_COORDENADOR = (int)(30000 * TEMPO_BASE);
    private const int CONSOME_RECURSO_MIN = (int)(5000 * TEMPO_BASE);
    private const int CONSOME_RECURSO_MAX = (int)(15000 * TEMPO_BASE);

    private static Processo _coordenador;
    private static ConcurrentDictionary<int, Processo> processosAtivos = new();

    public static void Main()
    {
        CriarProcessos();
        InativarCoordenador();
        AcessarRecurso();
    }

    public static void CriarProcessos()
    {
        new Thread(() =>
        {
            while (true)
            {
                int pid = 0;
                do
                {
                    pid = new Random().Next(1000);
                }
                while (!processosAtivos.TryAdd(pid, null));

                Processo processo = new(pid);
                processosAtivos[pid] = processo;

                Console.WriteLine($"Processo {pid} criado");

                Thread.Sleep(ADICIONA);
            }
        }).Start();
    }

    public static void InativarCoordenador()
    {
        new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(INATIVO_COORDENADOR);

                if (_coordenador == null)
                    continue;

                int pid = _coordenador?.Pid ?? 0;
                _coordenador?.Dispose();
                _coordenador = null;

                Console.WriteLine($"Coordenador {pid} morreu");
            }
        }).Start();
    }

    public static void AcessarRecurso()
    {
        new Thread(() =>
        {
            while (true)
            {
                if (processosAtivos.IsEmpty)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int intervalo = new Random().Next(CONSOME_RECURSO_MIN, CONSOME_RECURSO_MAX);
                Thread.Sleep(intervalo);

                ThreadPool.QueueUserWorkItem(async (_) =>
                {
                    int[] pids = processosAtivos.Keys.ToArray();
                    if (pids.Length == 0)
                        return;

                    int indexProcessoAleatorio = new Random().Next(pids.Length);
                    int pid = pids[indexProcessoAleatorio];

                    if (!processosAtivos.TryRemove(pid, out Processo processo))
                    {
                        Console.WriteLine("Erro ao selecionar processo para acessar recurso");
                        return;
                    }

                    await processo.AcessarRecursoCompartilhado();

                    if (processo.EhCoordenador)
                        _coordenador = processo;
                    else
                        processo?.Dispose();
                });
            }
        }).Start();
    }
}
