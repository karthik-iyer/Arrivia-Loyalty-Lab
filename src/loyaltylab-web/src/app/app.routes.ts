import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'offers' },
  {
    path: 'offers',
    loadComponent: () => import('./features/catalog/catalog-page').then((m) => m.CatalogPage),
  },
  {
    path: 'offers/:id',
    loadComponent: () =>
      import('./features/offer-detail/offer-detail-page').then((m) => m.OfferDetailPage),
  },
  {
    path: 'checkout/:quoteId',
    loadComponent: () => import('./features/checkout/checkout-page').then((m) => m.CheckoutPage),
  },
  {
    path: 'wallet',
    loadComponent: () => import('./features/wallet/wallet-page').then((m) => m.WalletPage),
  },
  {
    path: 'operator/sagas',
    loadComponent: () =>
      import('./features/operator/operator-list-page').then((m) => m.OperatorListPage),
  },
  {
    path: 'operator/sagas/:id',
    loadComponent: () =>
      import('./features/operator/operator-detail-page').then((m) => m.OperatorDetailPage),
  },
];
