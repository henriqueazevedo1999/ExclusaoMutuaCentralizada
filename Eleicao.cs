namespace ExclusaoMutua;

public class Eleicao
{
    public Processo RealizarEleicao(int idProcessoIniciador)
    {
        List<int> processosConsultados = new();

        foreach (Processo p in ControladorDeProcessos.ProcessosAtivos)
            processosConsultados.Add(p.getPid());

        int idNovoCoordenador = Math.Max(idProcessoIniciador, processosConsultados.Max());

        Processo coordenador = AtualizarCoordenador(idNovoCoordenador);

        if (coordenador != null)
            return coordenador;

        return ControladorDeProcessos.ProcessosAtivos.Single(p => p.getPid() == idProcessoIniciador);
    }

    private static Processo AtualizarCoordenador(int idNovoCoordenador)
    {
        Processo coordenador = null;
        foreach (Processo p in ControladorDeProcessos.ProcessosAtivos)
        {
            if (p.getPid() == idNovoCoordenador)
            {
                p.SetEhCoordenador(true);
                coordenador = p;
            }
            else
            {
                p.SetEhCoordenador(false);
            }
        }

        return coordenador;
    }

}