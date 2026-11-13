import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ConsultaPedidos, PaginaDePedidos } from '../modelos/pedido';

@Injectable({ providedIn: 'root' })
export class PedidosApi {
  private readonly http = inject(HttpClient);

  listar(consulta: ConsultaPedidos): Observable<PaginaDePedidos> {
    let params = new HttpParams()
      .set('pagina', consulta.pagina)
      .set('tamanhoPagina', consulta.tamanhoPagina);

    if (consulta.clienteId !== null) {
      params = params.set('clienteId', consulta.clienteId);
    }
    if (consulta.dataInicio) {
      params = params.set('dataInicio', consulta.dataInicio);
    }
    if (consulta.dataFim) {
      params = params.set('dataFim', consulta.dataFim);
    }

    return this.http.get<PaginaDePedidos>('/api/pedidos', { params });
  }
}
