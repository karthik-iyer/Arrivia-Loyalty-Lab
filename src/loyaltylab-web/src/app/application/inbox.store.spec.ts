import { TestBed } from '@angular/core/testing';

import { err, INBOX_PORT, ok } from '../domain';
import { InboxStore } from './inbox.store';
import { ActionNudgeUseCase, DismissNudgeUseCase, ListInboxUseCase } from './inbox.use-case';
import { coralNudge } from './test-fixtures';

describe('InboxStore', () => {
  it('loads delivered nudges from the inbox port', async () => {
    TestBed.configureTestingModule({
      providers: [
        InboxStore,
        ListInboxUseCase,
        ActionNudgeUseCase,
        DismissNudgeUseCase,
        {
          provide: INBOX_PORT,
          useValue: {
            list: async () => ok([coralNudge]),
            action: async () => {
              throw new Error('action should not run in this test');
            },
            dismiss: async () => {
              throw new Error('dismiss should not run in this test');
            },
          },
        },
      ],
    });

    const store = TestBed.inject(InboxStore);
    await store.load();

    expect(store.status()).toBe('ready');
    expect(store.nudges()[0]?.propertyName).toBe('Coral Bay Resort');
    expect(store.nudges()[0]?.signals[0]?.kind).toBe('WindowFit');
  });

  it('returns a live quote id on action and drops the card', async () => {
    TestBed.configureTestingModule({
      providers: [
        InboxStore,
        ListInboxUseCase,
        ActionNudgeUseCase,
        DismissNudgeUseCase,
        {
          provide: INBOX_PORT,
          useValue: {
            list: async () => ok([coralNudge]),
            action: async () =>
              ok({
                quoteId: 'q-live',
                offerId: coralNudge.offerId,
                memberPrice: { amount: 120.75, currency: 'USD' },
                maxCreditTender: { amount: 48.3, currency: 'USD' },
                maxCredits: 4830,
                expiresAt: '2026-03-15T12:15:00+00:00',
              }),
            dismiss: async () => ok(undefined),
          },
        },
      ],
    });

    const store = TestBed.inject(InboxStore);
    await store.load();
    const quoteId = await store.action(coralNudge.nudgeId);

    expect(quoteId).toBe('q-live');
    expect(store.nudges()).toEqual([]);
  });

  it('fades an expired nudge instead of treating it as a generic error', async () => {
    TestBed.configureTestingModule({
      providers: [
        InboxStore,
        ListInboxUseCase,
        ActionNudgeUseCase,
        DismissNudgeUseCase,
        {
          provide: INBOX_PORT,
          useValue: {
            list: async () => ok([coralNudge]),
            action: async () =>
              err({
                errorCode: 'NUDGE_EXPIRED',
                message: 'The nudge has expired and is no longer actionable.',
                status: 410,
                correlationId: 'c1',
              }),
            dismiss: async () => ok(undefined),
          },
        },
      ],
    });

    const store = TestBed.inject(InboxStore);
    await store.load();
    const quoteId = await store.action(coralNudge.nudgeId);

    expect(quoteId).toBeNull();
    expect(store.isExpired(coralNudge.nudgeId)).toBe(true);
    expect(store.error()).toBeNull();
    expect(store.nudges()).toHaveLength(1);
  });
});
