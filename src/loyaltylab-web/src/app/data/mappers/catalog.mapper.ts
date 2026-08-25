import type { OfferSummary } from '../../domain';
import type { OfferDto } from '../dto/catalog.dto';
import { toMoneyOrNull } from './money.mapper';

export function toOfferSummary(dto: OfferDto): OfferSummary {
  return {
    offerId: dto.offerId,
    propertyName: dto.propertyName,
    destination: dto.destinationName,
    destinationCode: dto.destinationCode,
    starRating: dto.starRating,
    tags: dto.tags,
    availableFrom: dto.availableFrom,
    availableTo: dto.availableTo,
    memberPrice: toMoneyOrNull(dto.memberPrice),
  };
}
