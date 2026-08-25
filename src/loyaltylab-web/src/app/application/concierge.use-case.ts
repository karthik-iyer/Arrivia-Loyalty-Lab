import { inject, Injectable } from '@angular/core';

import { CONCIERGE_PORT, type ConciergeRequest, type ConciergeView, type Result } from '../domain';

@Injectable({ providedIn: 'root' })
export class RecommendUseCase {
  private readonly concierge = inject(CONCIERGE_PORT);

  execute(request: ConciergeRequest): Promise<Result<ConciergeView>> {
    return this.concierge.recommend(request);
  }
}
