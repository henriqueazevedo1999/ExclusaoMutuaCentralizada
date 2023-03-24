using System.Text.Json;

namespace ExclusaoMutua;

public enum TipoMensagem
{
    Requisicao,
    Negacao,
    Concessao,
    Liberacao
}

public record Mensagem
{
    public Mensagem(int pid, TipoMensagem tipo)
    {
        Pid = pid;
        Tipo = tipo;
    }

    public int Pid { get; set; }
    public TipoMensagem Tipo { get; set; }

    public override string ToString()
    {
        JsonSerializerOptions options = new();
        options.WriteIndented = false;

        return JsonSerializer.Serialize(this, options);
    }
}
