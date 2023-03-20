using ExclusaoMutua;

public static class Program
{
    private static int ADICIONA = 4000;
    private static int INATIVO_PROCESSO = 8000;
    private static int INATIVO_COORDENADOR = 30000;
    private static int CONSOME_RECURSO_MIN = 5000;
    private static int CONSOME_RECURSO_MAX = 10000;

    private static object _lock = new object();

    public static void Main(string[] args)
    {
        CriarProcessos(ControladorDeProcessos.ProcessosAtivos);
        InativarCoordenador(ControladorDeProcessos.ProcessosAtivos);
        InativarProcesso(ControladorDeProcessos.ProcessosAtivos);
        AcessarRecurso(ControladorDeProcessos.ProcessosAtivos);
    }


    public static void CriarProcessos(List<Processo> processosAtivos)
    {
        new Thread(() =>
        {
            while (true)
            {
                lock (_lock)
                {
                    Processo processo = new Processo(GerarIdUnico(processosAtivos));

                    if (processosAtivos.Count == 0)
                    {
                        processo.EhCoordenador = true;
                    }

                    processosAtivos.Add(processo);
                }

                Esperar(ADICIONA);
            }
        }).Start();
    }


    private static int GerarIdUnico(List<Processo> processosAtivos)
    {
        Random random = new Random();
        int idRandom = random.Next(1000);

        foreach (Processo p in processosAtivos)
        {
            if (p.getPid() == idRandom)
                return GerarIdUnico(processosAtivos);
        }

        return idRandom;
    }

    public static void InativarProcesso(List<Processo> processosAtivos)
    {
        new Thread(() =>
        {
            while (true)
            {
                Esperar(INATIVO_PROCESSO);

                lock (_lock)
                {
                    if (processosAtivos.Count != 0)
                    {
                        int indexProcessoAleatorio = new Random().Next(processosAtivos.Count);
                        Processo pRemover = processosAtivos[indexProcessoAleatorio];
                        if (pRemover != null && !pRemover.isCoordenador())
                        {
                            pRemover.Destruir();
                        }
                    }
                }
            }
        }).Start();
    }

    public static void InativarCoordenador(List<Processo> processosAtivos)
    {
        new Thread(() =>
        {
            while (true)
            {
                Esperar(INATIVO_COORDENADOR);

                lock (_lock)
                {
                    Processo coordenador = null;
                    foreach (Processo p in processosAtivos)
                    {
                        if (p.IsCoordenador())
                        {
                            coordenador = p;
                        }
                    }
                    if (coordenador != null)
                    {
                        coordenador.Destruir();
                        Console.WriteLine("Processo coordenador " + coordenador + " destruido.");
                    }
                }
            }
        }).Start();
    }

    public static void AcessarRecurso(List<Processo> processosAtivos)
    {
        new Thread(() =>
        {
            Random random = new Random();
            int intervalo = 0;
            while (true)
            {
                intervalo = random.Next(CONSOME_RECURSO_MIN, CONSOME_RECURSO_MAX);
                Esperar(intervalo);

                lock (_lock)
                {
                    if (processosAtivos.Count > 0)
                    {
                        int indexProcessoAleatorio = new Random().Next(processosAtivos.Count);

                        Processo processoConsumidor = processosAtivos[indexProcessoAleatorio];
                        processoConsumidor.AcessarRecursoCompartilhado();
                    }
                }
            }
        }).Start();
    }


    private static void Esperar(int segundos)
    {
        Thread.Sleep(segundos);
    }
}
