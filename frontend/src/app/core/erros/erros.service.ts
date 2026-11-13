import { Injectable, signal } from '@angular/core';
import { ApiError } from '../api/api-error';

/**
 * Guarda o erro da última chamada de UMA tela. Não é `providedIn: 'root'`: cada tela o declara em `providers`, então
 * a instância nasce e morre com ela e erros antigos nunca vazam para outras rotas.
 */
@Injectable()
export class ErrosService {
  private readonly _erro = signal<ApiError | null>(null);

  readonly erro = this._erro.asReadonly();

  registrar(erro: ApiError): void {
    this._erro.set(erro);
  }

  limpar(): void {
    this._erro.set(null);
  }

  /** Mensagens do servidor para um campo, no formato que ele usa (ex.: `itens[0].quantidade`). */
  porCampo(campo: string): string[] {
    return this._erro()?.erros[campo] ?? [];
  }
}
