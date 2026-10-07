/** Pruebas unitarias (dominio y negocio) y e2e (API y auth contra PostgreSQL embebido). */
const shared = {
  testEnvironment: 'node',
  transform: { '^.+\\.ts$': ['ts-jest', { tsconfig: '<rootDir>/tsconfig.jest.json' }] },
  // Las pruebas usan el código fuente de los paquetes, sin necesidad de compilarlos antes.
  moduleNameMapper: { '^@atracciones/(.*)$': '<rootDir>/packages/$1/src' },
};

module.exports = {
  projects: [
    { ...shared, displayName: 'unit', testMatch: ['<rootDir>/packages/*/src/**/*.test.ts'] },
    {
      ...shared,
      displayName: 'e2e',
      testMatch: ['<rootDir>/apps/*/test/**/*.e2e.test.ts'],
      globalSetup: '<rootDir>/tools/test/global-setup.js',
      globalTeardown: '<rootDir>/tools/test/global-teardown.js',
    },
  ],
  testTimeout: 60000,
};
