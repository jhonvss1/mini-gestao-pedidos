import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  FormControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { paraApiError } from '../../../core/api/api-error';
import { CatalogoApi } from '../../../core/api/catalogo-api';
import { PedidosApi } from '../../../core/api/pedidos-api';
import { ErrosService } from '../../../core/erros/erros.service';
import { Cliente, Produto } from '../../../core/modelos/catalogo';
import { NotificacaoService } from '../../../core/notificacao/notificacao.service';
import { ErrosApi } from '../../../shared/erros-api/erros-api';
import { MoedaPipe } from '../../../shared/formatadores/moeda.pipe';

const QUANTIDADE_MAXIMA = 2_147_483_647;

@Component({
  selector: 'app-pedido-criar',
  imports: [ReactiveFormsModule, RouterLink, ErrosApi, MoedaPipe],
  // Instância própria por tela: os erros somem junto com ela (nada global vaza entre rotas).
  providers: [ErrosService],
  templateUrl: './pedido-criar.html',
  styleUrl: './pedido-criar.css',
})
export class PedidoCriar {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly pedidosApi = inject(PedidosApi);
  private readonly catalogoApi = inject(CatalogoApi);
  private readonly notificacao = inject(NotificacaoService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly moeda = new MoedaPipe();

  protected readonly erros = inject(ErrosService);
  protected readonly enviando = signal(false);
  protected readonly produtos = signal<Produto[]>([]);

  protected readonly clientes = toSignal(
    this.catalogoApi.clientes().pipe(
      catchError((erro) => {
        this.erros.registrar(paraApiError(erro));
        return of([] as Cliente[]);
      }),
    ),
    { initialValue: [] as Cliente[] },
  );

  protected readonly form = this.fb.group({
    clienteId: new FormControl<number | null>(null, { validators: [Validators.required] }),
    itens: this.fb.array([this.novaLinha()]),
  });

  private readonly valores = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  protected readonly opcoesClientes = computed(() => {
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

  protected readonly opcoesProdutos = computed(() =>
    this.produtos().map((produto) => ({
      id: produto.id,
      rotulo: `${produto.nome} — ${this.moeda.transform(produto.preco)} (${
        produto.estoque > 0 ? `estoque ${produto.estoque}` : 'sem estoque'
      })`,
    })),
  );

  /**
   * Estimativa pelo preço do cadastro, em centavos inteiros (nunca soma de números com casas decimais), só para
   * orientar o usuário. O valor final é calculado pelo servidor.
   */
  protected readonly totalEstimado = computed(() => {
    const precos = new Map(this.produtos().map((produto) => [produto.id, produto.preco]));
    const centavos = (this.valores().itens ?? []).reduce((soma, linha) => {
      const preco = linha?.produtoId != null ? precos.get(linha.produtoId) : undefined;
      const quantidade = Number(linha?.quantidade);
      const valida = preco !== undefined && Number.isInteger(quantidade) && quantidade >= 1;
      return valida ? soma + Math.round(preco * 100) * quantidade : soma;
    }, 0);

    return centavos / 100;
  });

  protected readonly nomeDoProduto = (produtoId: number): string =>
    this.produtos().find((produto) => produto.id === produtoId)?.nome ?? `Produto #${produtoId}`;

  constructor() {
    this.carregarProdutos();
  }

  protected get itens() {
    return this.form.controls.itens;
  }

  protected adicionarLinha(): void {
    this.itens.push(this.novaLinha());
  }

  protected removerLinha(indice: number): void {
    if (this.itens.length > 1) {
      this.itens.removeAt(indice);
    }
  }

  protected invalido(controle: { invalid: boolean; touched: boolean; dirty: boolean }): boolean {
    return controle.invalid && (controle.touched || controle.dirty);
  }

  protected enviar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { clienteId, itens } = this.form.getRawValue();
    this.erros.limpar();
    this.enviando.set(true);

    this.pedidosApi
      .criar({
        clienteId: clienteId!,
        itens: itens.map((linha) => ({
          produtoId: linha.produtoId!,
          quantidade: linha.quantidade,
        })),
      })
      .pipe(
        finalize(() => this.enviando.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (pedido) => {
          this.notificacao.publicar(`Pedido #${pedido.id} criado com sucesso.`);
          // A listagem nasce de novo: filtros limpos, página 1 e o pedido novo no topo (D-18).
          void this.router.navigate(['/pedidos']);
        },
        error: (erro) => {
          // O formulário NÃO é limpo: o usuário corrige e reenvia sem perder o preenchimento (CA-39).
          const apiError = paraApiError(erro);
          this.erros.registrar(apiError);
          if (apiError.codigo === 'ESTOQUE_INSUFICIENTE') {
            this.carregarProdutos(); // mostra o saldo atual nas opções
          }
        },
      });
  }

  private carregarProdutos(): void {
    this.catalogoApi
      .produtos()
      .pipe(
        catchError((erro) => {
          this.erros.registrar(paraApiError(erro));
          return of(null);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((produtos) => {
        if (produtos) {
          this.produtos.set(produtos);
        }
      });
  }

  private novaLinha() {
    return this.fb.group({
      produtoId: new FormControl<number | null>(null, { validators: [Validators.required] }),
      quantidade: this.fb.control(1, [
        Validators.required,
        Validators.pattern(/^[1-9]\d*$/),
        Validators.max(QUANTIDADE_MAXIMA),
      ]),
    });
  }
}
