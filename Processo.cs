namespace ExclusaoMutua;

public class Processo
{
    private Thread _threadRecurso;
    private Conexao _conexao = new Conexao();

    private List<Processo> _listaDeEspera;
    private bool recursoEmUso;

    private const int USO_PROCESSO_MIN = 10000;
    private const int USO_PROCESSO_MAX = 20000;

    public Processo(int pid)
    {
        Pid = pid;
    }

    public int Pid { get; init; }

    private bool _ehCoordenador;
    public bool EhCoordenador
    {
        get => _ehCoordenador;
        public set
        {
            _ehCoordenador = value;
            if (_ehCoordenador)
            {
                _listaDeEspera = new();
                _conexao.Conectar(this);

                if (ControladorDeProcessos.IsSendoConsumido())
                    ControladorDeProcessos.Consumidor.InterromperAcessoRecurso();

                recursoEmUso = false;
            }
        }
    }

    private void InterromperAcessoRecurso()
    {
        if (_threadRecurso?.IsAlive == true)
            _threadRecurso.Interrupt();
    }

    public bool IsRecursoEmUso()
    {
        return EncontrarCoordenador().recursoEmUso;
    }

    public void SetRecursoEmUso(Processo consumidor)
    {
        Processo coordenador = EncontrarCoordenador();

        coordenador.recursoEmUso = consumidor is not null;
        ControladorDeProcessos.Consumidor = consumidor;
    }

    private List<Processo> GetListaDeEspera()
    {
        return EncontrarCoordenador()._listaDeEspera;
    }

    public bool IsListaDeEsperaVazia()
    {
        return GetListaDeEspera().Count == 0;
    }

    private void RemoverDaListaDeEspera(Processo processo)
    {
        if (GetListaDeEspera().Any(p => p.Pid == processo.Pid))
            GetListaDeEspera().RemoveAll(p => p.Pid == processo.Pid);
    }

    private Processo EncontrarCoordenador()
    {
        Processo coordenador = ControladorDeProcessos.GetCoordenador();

        if (coordenador != null)
            return coordenador;

        Eleicao eleicao = new();
        return eleicao.RealizarEleicao(Pid);
    }

    public void AcessarRecursoCompartilhado()
    {
        if (ControladorDeProcessos.IsUsandoRecurso(this) || EhCoordenador)
            return;

        string resultado = Conexao.RealizarRequisicao($"Processo {Pid} quer consumir o recurso.\n");

        Console.WriteLine($"Resultado da requisicao do processo {Pid}: {resultado}");

        if (resultado == Conexao.PERMITIR_ACESSO)
            UtilizarRecurso(this);
        else if (resultado == Conexao.NEGAR_ACESSO)
            AdicionarNaListaDeEspera(this);
    }

    private void AdicionarNaListaDeEspera(Processo processoEmEspera)
    {
        GetListaDeEspera().Add(processoEmEspera);

        Console.WriteLine($"Processo {Pid} foi adicionado na lista de espera.");
        Console.WriteLine($"Lista de espera: {GetListaDeEspera()}");
    }

    private void UtilizarRecurso(Processo processo)
    {
        Random random = new();
        int randomUsageTime = USO_PROCESSO_MIN + random.Next(USO_PROCESSO_MAX - USO_PROCESSO_MIN);

        _threadRecurso = new Thread(() =>
        {
            Console.WriteLine("Processo " + processo + " está consumindo o recurso.");
            SetRecursoEmUso(processo);

            try
            {
                Thread.Sleep(randomUsageTime);
            }
            catch (ThreadInterruptedException) { }

            Console.WriteLine("Processo " + processo + " parou de consumir o recurso.");
            processo.LiberarRecurso();
        });

        _threadRecurso.Start();
    }

    private void LiberarRecurso()
    {
        SetRecursoEmUso(null);

        if (IsListaDeEsperaVazia())
            return;

        var listaEspera = GetListaDeEspera();
        Processo processoEmEspera = listaEspera.First();
        listaEspera.RemoveAt(0);

        processoEmEspera.AcessarRecursoCompartilhado();
        Console.WriteLine($"Processo {processoEmEspera} foi removido da lista de espera.");
        Console.WriteLine("Lista de espera: " + listaEspera);
    }

    public void Destruir()
    {
        if (EhCoordenador)
        {
            _conexao.EncerrarConexao();
        }
        else
        {
            RemoverDaListaDeEspera(this);
            if (ControladorDeProcessos.IsUsandoRecurso(this))
            {
                InterromperAcessoRecurso();
                LiberarRecurso();
            }
        }

        ControladorDeProcessos.RemoverProcesso(this);
    }
}