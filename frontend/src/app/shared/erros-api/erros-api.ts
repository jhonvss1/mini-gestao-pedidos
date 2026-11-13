import { Component, inject, input } from '@angular/core';
import { ErrosService } from '../../core/erros/erros.service';

/** Exibe o erro registrado no `ErrosService` da tela (404, 409, 422, 400 e falhas gerais). */
@Component({
  selector: 'app-erros-api',
  templateUrl: './erros-api.html',
  styleUrl: './erros-api.css',
})
export class ErrosApi {
  protected readonly erros = inject(ErrosService);

  /** Traduz o id de produto do 409 para o nome mostrado ao usuário. */
  readonly nomeDoProduto = input<(produtoId: number) => string>((id) => `Produto #${id}`);
}
