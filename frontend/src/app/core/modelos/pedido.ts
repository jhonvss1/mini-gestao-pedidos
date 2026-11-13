export interface ItemPedidoListado {
  produtoId: number;
  produtoNome: string;
  quantidade: number;
  precoUnitario: number;
  subtotal: number;
}

export interface PedidoListado {
  id: number;
  clienteId: number;
  clienteNome: string;
  /** ISO 8601 em UTC (ex.: 2026-11-12T14:32:10Z). */
  dataCriacao: string;
  valorTotal: number;
  itens: ItemPedidoListado[];
}

export interface PaginaDePedidos {
  registros: PedidoListado[];
  pagina: number;
  tamanhoPagina: number;
  totalItens: number;
  totalPaginas: number;
}

/** Filtros aplicados pelo usuário. Datas no formato yyyy-MM-dd, como o input type=date entrega. */
export interface FiltrosAplicados {
  clienteId: number | null;
  dataInicio: string | null;
  dataFim: string | null;
}

export interface ConsultaPedidos extends FiltrosAplicados {
  pagina: number;
  tamanhoPagina: number;
}
