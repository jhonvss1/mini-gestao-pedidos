import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Cliente, Produto } from '../modelos/catalogo';

@Injectable({ providedIn: 'root' })
export class CatalogoApi {
  private readonly http = inject(HttpClient);

  clientes(): Observable<Cliente[]> {
    return this.http.get<Cliente[]>('/api/clientes');
  }

  produtos(): Observable<Produto[]> {
    return this.http.get<Produto[]>('/api/produtos');
  }
}
