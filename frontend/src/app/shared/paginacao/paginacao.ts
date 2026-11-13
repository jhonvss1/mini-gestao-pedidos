import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-paginacao',
  templateUrl: './paginacao.html',
  styleUrl: './paginacao.css',
})
export class Paginacao {
  readonly pagina = input.required<number>();
  readonly totalPaginas = input.required<number>();
  readonly totalItens = input.required<number>();
  readonly tamanhoPagina = input.required<number>();

  readonly paginaAlterada = output<number>();
  readonly tamanhoAlterado = output<number>();

  protected readonly tamanhos = [10, 20, 50, 100];

  protected alterarTamanho(evento: Event): void {
    this.tamanhoAlterado.emit(Number((evento.target as HTMLSelectElement).value));
  }
}
