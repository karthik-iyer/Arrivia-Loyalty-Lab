// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

const layerBoundary = (disallow, message) => [
  'error',
  {
    patterns: [
      {
        regex: disallow,
        message,
      },
    ],
  },
];

module.exports = defineConfig([
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'll',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'll',
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    files: ['src/app/features/**/*.ts'],
    rules: {
      'no-restricted-imports': layerBoundary(
        String.raw`(^|/)data(/|$)|@angular/common/http`,
        'features/ may not import data/ or HttpClient. Inject a store from application/ (NFR-09).',
      ),
    },
  },
  {
    files: ['src/app/application/**/*.ts'],
    rules: {
      'no-restricted-imports': layerBoundary(
        String.raw`(^|/)data(/|$)|(^|/)features(/|$)|@angular/common/http`,
        'application/ may import domain/ only — not data/, features/, or HttpClient (NFR-09).',
      ),
    },
  },
  {
    files: ['src/app/domain/**/*.ts'],
    rules: {
      'no-restricted-imports': layerBoundary(
        String.raw`^@angular(/|$)|^rxjs(/|$)`,
        'domain/ is plain TypeScript: no Angular and no RxJS (NFR-09).',
      ),
    },
  },
  {
    files: ['src/app/data/**/*.ts'],
    rules: {
      'no-restricted-imports': layerBoundary(
        String.raw`(^|/)application(/|$)|(^|/)features(/|$)`,
        'data/ may import domain/ only — not application/ or features/ (NFR-09).',
      ),
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {},
  },
]);
