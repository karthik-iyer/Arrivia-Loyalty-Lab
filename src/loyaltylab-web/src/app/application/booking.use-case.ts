import { inject, Injectable } from '@angular/core';

import {
  BOOKING_PORT,
  WALLET_PORT,
  type BookingView,
  type CreateBookingOptions,
  type CreateBookingRequest,
  type Result,
  type WalletBalanceView,
} from '../domain';

@Injectable({ providedIn: 'root' })
export class StartBookingUseCase {
  private readonly bookings = inject(BOOKING_PORT);

  execute(
    request: CreateBookingRequest,
    idempotencyKey: string,
    options?: CreateBookingOptions,
  ): Promise<Result<BookingView>> {
    return this.bookings.create(request, idempotencyKey, options);
  }
}

@Injectable({ providedIn: 'root' })
export class GetBookingUseCase {
  private readonly bookings = inject(BOOKING_PORT);

  execute(bookingId: string): Promise<Result<BookingView>> {
    return this.bookings.get(bookingId);
  }
}

@Injectable({ providedIn: 'root' })
export class CancelBookingUseCase {
  private readonly bookings = inject(BOOKING_PORT);

  execute(bookingId: string, idempotencyKey: string): Promise<Result<BookingView>> {
    return this.bookings.cancel(bookingId, idempotencyKey);
  }
}

@Injectable({ providedIn: 'root' })
export class GetBalanceUseCase {
  private readonly wallet = inject(WALLET_PORT);

  execute(): Promise<Result<WalletBalanceView>> {
    return this.wallet.balance();
  }
}
