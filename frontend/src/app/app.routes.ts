import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'pedidos' },
  {
    path: 'pedidos',
    loadComponent: () =>
      import('./features/pedidos/lista/pedidos-lista').then((m) => m.PedidosLista),
  },
  {
    path: 'pedidos/novo',
    loadComponent: () => import('./features/pedidos/criar/pedido-criar').then((m) => m.PedidoCriar),
  },
  { path: '**', redirectTo: 'pedidos' },
];
