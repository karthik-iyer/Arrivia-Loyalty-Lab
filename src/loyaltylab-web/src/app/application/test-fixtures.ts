import type {
  BookingView,
  OfferSummary,
  PriceExplanationView,
  WalletBalanceView,
  WalletStatementView,
} from '../domain';

export const coralOffer: OfferSummary = {
  offerId: 'a11ce001-0004-7000-8000-000000000001',
  propertyName: 'Coral Bay Resort',
  destination: 'Montego Bay',
  destinationCode: 'MBJ',
  starRating: 4,
  tags: ['Beach', 'Family'],
  availableFrom: '2026-01-01',
  availableTo: '2026-06-30',
  memberPrice: { amount: 120.75, currency: 'USD' },
};

export const anonymousCoral: OfferSummary = { ...coralOffer, memberPrice: null };

export const mayaBalance: WalletBalanceView = {
  memberId: 'a11ce001-0002-7000-8000-000000000001',
  credits: 6000,
  monetaryValue: { amount: 60, currency: 'USD' },
  burnCap: 40,
};

/** Burn that a later reversal must link back to (FR-L-12 / US-04). */
export const originalBurnId = 'a11ce001-0009-7000-8000-000000000002';

export const mayaStatement: WalletStatementView = {
  memberId: mayaBalance.memberId,
  balance: 6000,
  lines: [
    {
      id: 'a11ce001-0009-7000-8000-000000000001',
      type: 'Earn',
      occurredAt: '2026-03-01T00:00:00+00:00',
      reason: 'Opening grant',
      credits: 6000,
      runningBalance: 6000,
      reversesTransactionId: null,
    },
    {
      id: originalBurnId,
      type: 'Burn',
      occurredAt: '2026-03-15T12:00:00+00:00',
      reason: 'Booking tender',
      credits: -4830,
      runningBalance: 1170,
      reversesTransactionId: null,
    },
    {
      id: 'a11ce001-0009-7000-8000-000000000003',
      type: 'Reversal',
      occurredAt: '2026-03-15T12:04:00+00:00',
      reason: 'Capture failed',
      credits: 4830,
      runningBalance: 6000,
      reversesTransactionId: originalBurnId,
    },
  ],
};

export const clampedExplanation: PriceExplanationView = {
  stages: [
    {
      stage: 'BaseMarkup',
      order: 3,
      description: 'Partner markup 12%',
      appliedRule: null,
      subtotalBefore: { amount: 0, currency: 'USD' },
      subtotalAfter: { amount: 13.8, currency: 'USD' },
      wasClamped: false,
      clampReason: null,
    },
    {
      stage: 'MarginFloor',
      order: 6,
      description: 'Partner minimum margin',
      appliedRule: null,
      subtotalBefore: { amount: 4.83, currency: 'USD' },
      subtotalAfter: { amount: 5.75, currency: 'USD' },
      wasClamped: true,
      clampReason: 'Raised by 0.92 to meet the partner minimum.',
    },
  ],
  memberPrice: { amount: 120.75, currency: 'USD' },
  maxCreditTender: { amount: 48.3, currency: 'USD' },
  netCost: null,
  margin: null,
};

function succeeded(kind: BookingView['saga']['steps'][number]['kind']): BookingView['saga']['steps'][number] {
  return {
    kind,
    status: 'Succeeded',
    attempts: 1,
    externalReference: null,
    error: null,
    durationMs: 12,
    compensation: null,
  };
}

export const compensatedBooking: BookingView = {
  bookingId: 'b-compensated',
  status: 'Failed',
  tender: { cash: { amount: 120.75, currency: 'USD' }, credits: 0 },
  drift: null,
  saga: {
    id: 's-compensated',
    status: 'Compensated',
    steps: [
      succeeded('ValidateQuote'),
      {
        kind: 'ReserveInventory',
        status: 'Compensated',
        attempts: 1,
        externalReference: 'OCE-88213',
        error: 'PAYMENT_DECLINED',
        durationMs: 40,
        compensation: { status: 'Succeeded', attempts: 1, externalReference: null, errorCode: null },
      },
      {
        kind: 'AuthorizePayment',
        status: 'Failed',
        attempts: 1,
        externalReference: null,
        error: 'PAYMENT_DECLINED',
        durationMs: 20,
        compensation: null,
      },
      { ...succeeded('BurnCredits'), status: 'Pending' },
      { ...succeeded('CapturePayment'), status: 'Pending' },
      { ...succeeded('ConfirmBooking'), status: 'Pending' },
    ],
  },
};

export const confirmedBooking: BookingView = {
  bookingId: 'b-ok',
  status: 'Confirmed',
  tender: { cash: { amount: 72.45, currency: 'USD' }, credits: 4830 },
  drift: null,
  saga: {
    id: 's-ok',
    status: 'Confirmed',
    steps: [
      succeeded('ValidateQuote'),
      succeeded('ReserveInventory'),
      succeeded('AuthorizePayment'),
      succeeded('BurnCredits'),
      succeeded('CapturePayment'),
      succeeded('ConfirmBooking'),
    ],
  },
};
