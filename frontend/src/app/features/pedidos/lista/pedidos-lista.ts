import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiError, paraApiError } from '../../../core/api/api-error';
import { CatalogoApi } from '../../../core/api/catalogo-api';
import { PedidosApi } from '../../../core/api/pedidos-api';
import { NotificacaoService } from '../../../core/notificacao/notificacao.service';
import { Cliente } from '../../../core/modelos/catalogo';
import { ConsultaPedidos, FiltrosAplicados, PaginaDePedidos } from '../../../core/modelos/pedido';
import { DataHoraPipe } from '../../../shared/formatadores/data-hora.pipe';
import { MoedaPipe } from '../../../shared/formatadores/moeda.pipe';
import { Paginacao } from '../../../shared/paginacao/paginacao';
import { PedidosFiltros } from './pedidos-filtros';

type EstadoLista =
  | { tipo: 'carregando' }
  | { tipo: 'ok'; pagina: PaginaDePedidos }
  | { tipo: 'vazio'; pagina: PaginaDePedidos }
  | { tipo: 'erro'; erro: ApiError };

const CONSULTA_INICIAL: ConsultaPedidos = {
  clienteId: null,
  dataInicio: null,
  dataFim: null,
  pagina: 1,
  tamanhoPagina: 20,
};

@Component({
  selector: 'app-pedidos-lista',
  imports: [PedidosFiltros, Paginacao, MoedaPipe, DataHoraPipe],
  templateUrl: './pedidos-lista.html',
  styleUrl: './pedidos-lista.css',
})
export class PedidosLista {
  private readonly pedidosApi = inject(PedidosApi);
  private readonly catalogoApi = inject(CatalogoApi);
  private readonly notificacao = inject(NotificacaoService);

  /** Aviso de uma tela anterior (ex.: pedido criado). Exibido uma única vez. */
  protected readonly aviso = signal<string | null>(this.notificacao.consumir());

  protected readonly consulta = signal<ConsultaPedidos>(CONSULTA_INICIAL);
  protected readonly estado = signal<EstadoLista>({ tipo: 'carregando' });

  /** Falha ao carregar clientes não bloqueia a listagem: o filtro fica só com "Todos". */
  protected readonly clientes = toSignal(
    this.catalogoApi.clientes().pipe(catchError(() => of([] as Cliente[]))),
    {
      initialValue: [] as Cliente[],
    },
  );

  constructor() {
    // switchMap cancela a requisição anterior quando os filtros ou a página mudam antes de ela terminar.
    toObservable(this.consulta)
      .pipe(
        switchMap((consulta) =>
          this.pedidosApi.listar(consulta).pipe(
            map((pagina): EstadoLista =>
              pagina.registros.length === 0 ? { tipo: 'vazio', pagina } : { tipo: 'ok', pagina },
            ),
            catchError((erro) => of<EstadoLista>({ tipo: 'erro', erro: paraApiError(erro) })),
            startWith<EstadoLista>({ tipo: 'carregando' }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((estado) => this.estado.set(estado));
  }

  protected aplicarFiltros(filtros: FiltrosAplicados): void {
    this.consulta.update((atual) => ({ ...atual, ...filtros, pagina: 1 }));
  }

  protected irParaPagina(pagina: number): void {
    this.consulta.update((atual) => ({ ...atual, pagina }));
  }

  protected alterarTamanho(tamanhoPagina: number): void {
    this.consulta.update((atual) => ({ ...atual, tamanhoPagina, pagina: 1 }));
  }

  protected tentarNovamente(): void {
    this.consulta.update((atual) => ({ ...atual }));
  }
}
