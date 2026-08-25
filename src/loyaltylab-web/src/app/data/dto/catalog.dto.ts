import type { OfferTag } from '../../domain';
import type { MoneyDto } from './money.dto';

export interface OfferDto {
  readonly offerId: string;
  readonly propertyName: string;
  readonly destinationCode: string;
  readonly destinationName: string;
  readonly starRating: number;
  readonly tags: readonly OfferTag[];
  readonly availableFrom: string;
  readonly availableTo: string;
  readonly memberPrice?: MoneyDto | null;
}
