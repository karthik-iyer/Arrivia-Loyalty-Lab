import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { BookingPort, BookingView, CreateBookingOptions, CreateBookingRequest, Result } from '../../domain';
import type { BookingDto } from '../dto/booking.dto';
import { toBookingView, toCreateBookingDto } from '../mappers/booking.mapper';
import { HttpResult } from './http-result';

@Injectable()
export class HttpBookingAdapter implements BookingPort {
  private readonly http = inject(HttpClient);
  private readonly results = inject(HttpResult);

  create(
    request: CreateBookingRequest,
    idempotencyKey: string,
    options?: CreateBookingOptions,
  ): Promise<Result<BookingView>> {
    return this.results.capture(async () => {
      const headers: Record<string, string> = { 'Idempotency-Key': idempotencyKey };
      if (options?.forcePaymentDecline) {
        headers['X-Fault-Profile'] = '{"paymentDecline":true}';
      }

      const dto = await firstValueFrom(
        this.http.post<BookingDto>('/api/bookings', toCreateBookingDto(request), { headers }),
      );
      return toBookingView(dto);
    });
  }

  get(bookingId: string): Promise<Result<BookingView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(this.http.get<BookingDto>(`/api/bookings/${bookingId}`));
      return toBookingView(dto);
    });
  }

  cancel(bookingId: string, idempotencyKey: string): Promise<Result<BookingView>> {
    return this.results.capture(async () => {
      const dto = await firstValueFrom(
        this.http.post<BookingDto>(`/api/bookings/${bookingId}/cancel`, {}, {
          headers: { 'Idempotency-Key': idempotencyKey },
        }),
      );
      return toBookingView(dto);
    });
  }
}
