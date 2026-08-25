import type { BookingView, OfferSummary, PriceExplanationView } from '../domain';

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
