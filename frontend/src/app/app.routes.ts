import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'pedidos' },
  {
    path: 'pedidos',
    loadComponent: () =>
      import('./features/pedidos/lista/pedidos-lista').then((m) => m.PedidosLista),
  },
  { path: '**', redirectTo: 'pedidos' },
];
