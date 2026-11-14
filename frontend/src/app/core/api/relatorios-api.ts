import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ClienteRanking, PeriodoRanking } from '../modelos/ranking';

@Injectable({ providedIn: 'root' })
export class RelatoriosApi {
  private readonly http = inject(HttpClient);

  /** Até 5 clientes por valor total no período, já ordenados pela API (total decrescente, id crescente). */
  topClientes(periodo: PeriodoRanking): Observable<ClienteRanking[]> {
    const params = new HttpParams()
      .set('dataInicio', periodo.dataInicio)
      .set('dataFim', periodo.dataFim);

    return this.http.get<ClienteRanking[]>('/api/relatorios/top-clientes', { params });
  }
}
