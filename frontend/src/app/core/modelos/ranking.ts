export interface ClienteRanking {
  clienteId: number;
  nome: string;
  valorTotal: number;
}

/** Período do ranking: datas yyyy-MM-dd inclusivas, como o input type=date entrega. */
export interface PeriodoRanking {
  dataInicio: string;
  dataFim: string;
}
