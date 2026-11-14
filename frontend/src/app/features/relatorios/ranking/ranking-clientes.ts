import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiError, paraApiError } from '../../../core/api/api-error';
import { RelatoriosApi } from '../../../core/api/relatorios-api';
import { ClienteRanking, PeriodoRanking } from '../../../core/modelos/ranking';
import { MoedaPipe } from '../../../shared/formatadores/moeda.pipe';

type EstadoRanking =
  | { tipo: 'carregando' }
  | { tipo: 'ok'; clientes: ClienteRanking[] }
  | { tipo: 'vazio' }
  | { tipo: 'erro'; erro: ApiError };

/** yyyy-MM-dd pelo calendário local (toISOString converteria para UTC e poderia mudar o dia). */
function paraDataIso(data: Date): string {
  const mes = String(data.getMonth() + 1).padStart(2, '0');
  const dia = String(data.getDate()).padStart(2, '0');
  return `${data.getFullYear()}-${mes}-${dia}`;
}

/** Padrão ao abrir a tela: do primeiro dia do mês corrente até hoje. */
function periodoInicial(): PeriodoRanking {
  const hoje = new Date();
  return {
    dataInicio: paraDataIso(new Date(hoje.getFullYear(), hoje.getMonth(), 1)),
    dataFim: paraDataIso(hoje),
  };
}

@Component({
  selector: 'app-ranking-clientes',
  imports: [ReactiveFormsModule, MoedaPipe],
  templateUrl: './ranking-clientes.html',
  styleUrl: './ranking-clientes.css',
})
export class RankingClientes {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly relatoriosApi = inject(RelatoriosApi);

  private readonly inicial = periodoInicial();

  protected readonly periodo = signal<PeriodoRanking>(this.inicial);
  protected readonly estado = signal<EstadoRanking>({ tipo: 'carregando' });
  protected readonly erroIntervalo = signal<string | null>(null);

  protected readonly form = this.fb.group({
    dataInicio: [this.inicial.dataInicio, Validators.required],
    dataFim: [this.inicial.dataFim, Validators.required],
  });

  /** Maior total do período: base da barra proporcional de cada linha. */
  protected readonly maiorTotal = computed(() => {
    const atual = this.estado();
    return atual.tipo === 'ok' ? Math.max(...atual.clientes.map((c) => c.valorTotal)) : 0;
  });

  constructor() {
    // switchMap cancela a consulta anterior quando o período muda antes de ela terminar.
    toObservable(this.periodo)
      .pipe(
        switchMap((periodo) =>
          this.relatoriosApi.topClientes(periodo).pipe(
            map((clientes): EstadoRanking =>
              clientes.length === 0 ? { tipo: 'vazio' } : { tipo: 'ok', clientes },
            ),
            catchError((erro) => of<EstadoRanking>({ tipo: 'erro', erro: paraApiError(erro) })),
            startWith<EstadoRanking>({ tipo: 'carregando' }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((estado) => this.estado.set(estado));
  }

  protected consultar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.erroIntervalo.set(null);
      return;
    }

    const { dataInicio, dataFim } = this.form.getRawValue();

    // yyyy-MM-dd ordena como texto: compara sem converter datas (e sem problema de fuso).
    if (dataInicio > dataFim) {
      this.erroIntervalo.set('A data inicial não pode ser posterior à data final.');
      return;
    }

    this.erroIntervalo.set(null);
    this.periodo.set({ dataInicio, dataFim });
  }

  protected tentarNovamente(): void {
    this.periodo.update((atual) => ({ ...atual }));
  }

  protected percentual(cliente: ClienteRanking): number {
    const maior = this.maiorTotal();
    return maior > 0 ? Math.round((cliente.valorTotal / maior) * 100) : 0;
  }

  protected dataBr(iso: string): string {
    return iso.split('-').reverse().join('/');
  }
}
