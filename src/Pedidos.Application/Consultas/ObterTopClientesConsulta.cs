namespace Pedidos.Application.Consultas;

/// <summary>Período obrigatório do ranking (RN-22). Ausente é erro: ambos os campos são anuláveis só para reportá-lo.</summary>
public record ObterTopClientesConsulta(DateOnly? DataInicio, DateOnly? DataFim);
