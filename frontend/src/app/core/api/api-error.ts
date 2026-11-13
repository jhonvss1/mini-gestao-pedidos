import { HttpErrorResponse } from '@angular/common/http';

export interface ProdutoComProblema {
  produtoId: number;
  /** 404: mensagem do servidor identificando o item. */
  mensagem?: string;
  /** 409: quantidades solicitada e disponível. */
  solicitado?: number;
  disponivel?: number;
}

/** Erro da API já traduzido do Problem Details (spec 4.1), pronto para exibir. */
export interface ApiError {
  status: number;
  codigo: string | null;
  titulo: string;
  detalhe: string | null;
  /** 400/422: campo (ex.: itens[0].quantidade) → mensagens. */
  erros: Record<string, string[]>;
  /** 404/409: produtos afetados. */
  produtos: ProdutoComProblema[];
  /** Texto legível para exibição geral. */
  mensagem: string;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  codigo?: string;
  errors?: Record<string, string[]>;
  produtos?: ProdutoComProblema[];
}

const MENSAGEM_REDE =
  'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.';
const MENSAGEM_INESPERADO = 'Ocorreu um erro inesperado no servidor. Tente novamente em instantes.';

export function paraApiError(erro: unknown): ApiError {
  if (!(erro instanceof HttpErrorResponse)) {
    return semDetalhes(0, MENSAGEM_INESPERADO);
  }

  if (erro.status === 0) {
    return semDetalhes(0, MENSAGEM_REDE);
  }

  // 5xx: o servidor não expõe detalhes internos; a mensagem é sempre genérica.
  if (erro.status >= 500) {
    return semDetalhes(erro.status, MENSAGEM_INESPERADO);
  }

  const corpo: ProblemDetails = erro.error && typeof erro.error === 'object' ? erro.error : {};
  const erros = corpo.errors ?? {};
  const titulo = corpo.title ?? 'Requisição não atendida';
  const detalhe = corpo.detail ?? null;

  return {
    status: erro.status,
    codigo: corpo.codigo ?? null,
    titulo,
    detalhe,
    erros,
    produtos: corpo.produtos ?? [],
    mensagem: montarMensagem(titulo, detalhe, erros),
  };
}

function semDetalhes(status: number, mensagem: string): ApiError {
  return {
    status,
    codigo: null,
    titulo: mensagem,
    detalhe: null,
    erros: {},
    produtos: [],
    mensagem,
  };
}

function montarMensagem(
  titulo: string,
  detalhe: string | null,
  erros: Record<string, string[]>,
): string {
  if (detalhe) {
    return detalhe;
  }

  const mensagens = Object.values(erros).flat();
  return mensagens.length > 0 ? mensagens.join(' ') : titulo;
}
