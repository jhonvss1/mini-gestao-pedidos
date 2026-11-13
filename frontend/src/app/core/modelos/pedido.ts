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

export interface ItemRequisicao {
  produtoId: number;
  quantidade: number;
}

/** Corpo do POST /api/pedidos. Não há preço: ele vem do cadastro do produto (RN-04). */
export interface CriarPedidoRequisicao {
  clienteId: number;
  itens: ItemRequisicao[];
}

export interface ItemPedidoCriado {
  produtoId: number;
  quantidade: number;
  precoUnitario: number;
  subtotal: number;
}

export interface PedidoCriado {
  id: number;
  clienteId: number;
  dataCriacao: string;
  valorTotal: number;
  itens: ItemPedidoCriado[];
}
