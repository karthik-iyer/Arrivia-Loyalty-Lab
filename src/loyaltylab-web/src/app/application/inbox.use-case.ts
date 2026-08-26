import { inject, Injectable } from '@angular/core';

import { INBOX_PORT, type NudgeView, type QuoteView, type Result } from '../domain';

@Injectable({ providedIn: 'root' })
export class ListInboxUseCase {
  private readonly inbox = inject(INBOX_PORT);

  execute(): Promise<Result<readonly NudgeView[]>> {
    return this.inbox.list();
  }
}

@Injectable({ providedIn: 'root' })
export class ActionNudgeUseCase {
  private readonly inbox = inject(INBOX_PORT);

  execute(nudgeId: string): Promise<Result<QuoteView>> {
    return this.inbox.action(nudgeId);
  }
}

@Injectable({ providedIn: 'root' })
export class DismissNudgeUseCase {
  private readonly inbox = inject(INBOX_PORT);

  execute(nudgeId: string): Promise<Result<void>> {
    return this.inbox.dismiss(nudgeId);
  }
}
