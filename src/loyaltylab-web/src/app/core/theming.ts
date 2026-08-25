import type { PartnerThemeView } from '../domain';

const hexColor = /^#[0-9A-Fa-f]{6}$/;

export function isCssColor(value: string): boolean {
  return hexColor.test(value);
}

export function applyPartnerTheme(theme: PartnerThemeView | null, root: CSSStyleDeclaration): void {
  if (!theme) {
    return;
  }

  setIfValid(root, '--ll-color-primary', theme.primaryColor);
  setIfValid(root, '--ll-color-surface', theme.surfaceColor);
  setIfValid(root, '--ll-color-accent', theme.accentColor);
}

function setIfValid(root: CSSStyleDeclaration, property: string, value: string): void {
  if (isCssColor(value)) {
    root.setProperty(property, value.toUpperCase());
  }
}
