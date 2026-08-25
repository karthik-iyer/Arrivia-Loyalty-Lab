import type { DemoIdentity } from '../domain';

export const DEFAULT_PERSONA: DemoIdentity = {
  id: 'summit-maya',
  label: 'Maya · Summit Gold',
  partnerCode: 'SUMMIT',
  memberId: 'a11ce001-0002-7000-8000-000000000001',
  role: 'Member',
};

/** Seeded identities from DemoSeed — the switcher only selects these (FR-X-10). */
export const DEMO_PERSONAS: readonly DemoIdentity[] = [
  DEFAULT_PERSONA,
  {
    id: 'summit-ravi',
    label: 'Ravi · Summit Standard',
    partnerCode: 'SUMMIT',
    memberId: 'a11ce001-0002-7000-8000-000000000002',
    role: 'Member',
  },
  {
    id: 'summit-anon',
    label: 'Anonymous · Summit',
    partnerCode: 'SUMMIT',
    memberId: null,
    role: 'Anonymous',
  },
  {
    id: 'nimbus-chen',
    label: 'Chen · Nimbus',
    partnerCode: 'NIMBUS',
    memberId: 'a11ce001-0002-7000-8000-000000000003',
    role: 'Member',
  },
  {
    id: 'nimbus-anon',
    label: 'Anonymous · Nimbus',
    partnerCode: 'NIMBUS',
    memberId: null,
    role: 'Anonymous',
  },
  {
    id: 'summit-operator',
    label: 'Operator · Summit',
    partnerCode: 'SUMMIT',
    memberId: null,
    role: 'Operator',
  },
  {
    id: 'summit-finance',
    label: 'Finance · Summit',
    partnerCode: 'SUMMIT',
    memberId: null,
    role: 'FinanceAnalyst',
  },
];
