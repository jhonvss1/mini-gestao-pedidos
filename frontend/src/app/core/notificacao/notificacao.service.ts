import { Injectable } from '@angular/core';

/** Recado de uma tela para a próxima (ex.: "pedido criado" exibido na listagem). Lido uma única vez. */
@Injectable({ providedIn: 'root' })
export class NotificacaoService {
  private mensagem: string | null = null;

  publicar(mensagem: string): void {
    this.mensagem = mensagem;
  }

  consumir(): string | null {
    const atual = this.mensagem;
    this.mensagem = null;
    return atual;
  }
}
