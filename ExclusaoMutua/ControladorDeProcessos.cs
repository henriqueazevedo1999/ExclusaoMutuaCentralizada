namespace ExclusaoMutua;

public class ControladorDeProcessos
{
    public static List<Processo> ProcessosAtivos { get; private set; } = new List<Processo>();
    public static RecursoCompartilhado Recurso { get; private set; } = new RecursoCompartilhado();
    public static Processo Consumidor { get; set; }

    private ControladorDeProcessos() { }

    public static Processo GetCoordenador()
    {
        foreach (Processo processo in ProcessosAtivos)
        {
            if (processo.IsCoordenador())
            {
                return processo;
            }
        }
        return null;
    }

    public static void RemoverProcesso(Processo processo)
    {
        ProcessosAtivos.Remove(processo);
    }

    public static bool IsUsandoRecurso(Processo processo)
    {
        return processo.Pid == Consumidor.Pid;
    }

    public static bool IsSendoConsumido()
    {
        return Consumidor != null;
    }
}
