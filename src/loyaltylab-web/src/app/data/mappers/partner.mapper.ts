import type { PartnerThemeView } from '../../domain';
import type { PartnerThemeDto } from '../dto/partner.dto';

export function toPartnerThemeView(dto: PartnerThemeDto): PartnerThemeView {
  return {
    code: dto.code,
    displayName: dto.displayName,
    primaryColor: dto.primaryColor,
    surfaceColor: dto.surfaceColor,
    accentColor: dto.accentColor,
    logoUrl: dto.logoUrl ?? null,
  };
}
