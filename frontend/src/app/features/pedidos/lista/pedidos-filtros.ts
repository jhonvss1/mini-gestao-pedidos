import { Component, computed, inject, input, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Cliente } from '../../../core/modelos/catalogo';
import { FiltrosAplicados } from '../../../core/modelos/pedido';

@Component({
  selector: 'app-pedidos-filtros',
  imports: [ReactiveFormsModule],
  templateUrl: './pedidos-filtros.html',
  styleUrl: './pedidos-filtros.css',
})
export class PedidosFiltros {
  private readonly fb = inject(NonNullableFormBuilder);

  readonly clientes = input<Cliente[]>([]);
  readonly filtrar = output<FiltrosAplicados>();

  protected readonly erroIntervalo = signal<string | null>(null);

  protected readonly form = this.fb.group({
    clienteId: '',
    dataInicio: '',
    dataFim: '',
  });

  /** Clientes homônimos mostram o id no rótulo para o usuário distinguir um do outro. */
  protected readonly opcoes = computed(() => {
    const contagem = new Map<string, number>();
    for (const cliente of this.clientes()) {
      contagem.set(cliente.nome, (contagem.get(cliente.nome) ?? 0) + 1);
    }

    return this.clientes().map((cliente) => ({
      id: cliente.id,
      rotulo:
        (contagem.get(cliente.nome) ?? 0) > 1 ? `${cliente.nome} (#${cliente.id})` : cliente.nome,
    }));
  });

  protected aplicar(): void {
    const { clienteId, dataInicio, dataFim } = this.form.getRawValue();

    // yyyy-MM-dd ordena como texto: compara sem converter datas (e sem problema de fuso).
    if (dataInicio && dataFim && dataInicio > dataFim) {
      this.erroIntervalo.set('A data inicial não pode ser posterior à data final.');
      return;
    }

    this.erroIntervalo.set(null);
    this.filtrar.emit({
      clienteId: clienteId ? Number(clienteId) : null,
      dataInicio: dataInicio || null,
      dataFim: dataFim || null,
    });
  }

  protected limpar(): void {
    this.form.reset();
    this.erroIntervalo.set(null);
    this.filtrar.emit({ clienteId: null, dataInicio: null, dataFim: null });
  }
}
