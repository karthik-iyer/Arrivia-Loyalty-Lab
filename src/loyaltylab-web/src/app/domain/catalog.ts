import type { Money } from './money';

export type OfferTag = 'Beach' | 'Ski' | 'City' | 'Family' | 'Luxury';

export interface OfferSummary {
  readonly offerId: string;
  readonly propertyName: string;
  readonly destination: string;
  readonly destinationCode: string;
  readonly starRating: number;
  readonly tags: readonly OfferTag[];
  readonly availableFrom: string;
  readonly availableTo: string;
  /** Null when signed out — FR-X-05 is in the type. */
  readonly memberPrice: Money | null;
}
