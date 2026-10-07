// Arma out/backend: el API y dev-auth listos para ejecutar en Azure App Service, sin compilar en el servidor.
//
//   out/backend/apps/api/dist/main.js    (comando de inicio: node apps/api/dist/main.js)
//   out/backend/apps/auth/dist/main.js   (comando de inicio: node apps/auth/dist/main.js)
//   out/backend/package.json + node_modules con solo las dependencias de producción
//
// Los paquetes del monorepo (@atracciones/*) se incrustan en cada main.js: así node_modules no contiene los enlaces
// simbólicos de los workspaces, que App Service no conserva. Se empaqueta la salida de `tsc` (no el TypeScript), porque
// esbuild no emite los metadatos de decoradores que necesita NestJS.
//
// Uso: npm run build && node tools/package-backend.mjs   (instala dependencias de Linux si se ejecuta en Linux)
import { execSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const out = join(root, 'out', 'backend');
const workspaces = ['packages/domain', 'packages/data-management', 'packages/business', 'packages/contracts', 'packages/data-access', 'apps/api', 'apps/auth'];
const readJson = (file) => JSON.parse(readFileSync(join(root, file), 'utf8'));

for (const app of ['api', 'auth']) {
  if (!existsSync(join(root, 'apps', app, 'dist', 'main.js'))) throw new Error(`Falta apps/${app}/dist/main.js: ejecuta antes "npm run build".`);
}

// Dependencias de terceros de todos los workspaces, fijadas a la versión instalada (la del package-lock).
const dependencies = {};
for (const ws of workspaces) {
  for (const name of Object.keys(readJson(`${ws}/package.json`).dependencies ?? {})) {
    if (!name.startsWith('@atracciones/')) dependencies[name] = readJson(`node_modules/${name}/package.json`).version;
  }
}

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

for (const app of ['api', 'auth']) {
  await build({
    entryPoints: [join(root, 'apps', app, 'dist', 'main.js')],
    outfile: join(out, 'apps', app, 'dist', 'main.js'),
    bundle: true,
    platform: 'node',
    target: 'node22',
    format: 'cjs',
    sourcemap: true,
    // Todo lo que no es del monorepo se resuelve en node_modules en tiempo de ejecución (incluidos los require opcionales de NestJS).
    plugins: [{
      name: 'externals',
      setup(b) {
        b.onResolve({ filter: /^[^./]/ }, (args) =>
          args.kind === 'entry-point' || args.path.startsWith('@atracciones/') ? undefined : { path: args.path, external: true },
        );
      },
    }],
    logLevel: 'warning',
  });
}

const rootPkg = readJson('package.json');
writeFileSync(
  join(out, 'package.json'),
  `${JSON.stringify({ name: 'tourgirls-backend', version: rootPkg.version, private: true, engines: rootPkg.engines, dependencies }, null, 2)}\n`,
);
execSync('npm install --omit=dev --no-audit --no-fund --ignore-scripts', { cwd: out, stdio: 'inherit' });
console.log(`Listo: ${out}`);
