// Dev-server proxy: the browser bundle can't read Node env vars, so this file — which runs in the
// `ng serve` Node process — reads the gateway address Aspire injects (`WithReference(gateway)` in the
// AppHost sets `services__gateway__https__0` / `__http__0`) and forwards the app's `/api/*` calls to it.
// The gateway is the ONLY upstream, and its address is never hardcoded — it comes from injected config.
// The `^/api` prefix is stripped so `/api/jobs` reaches the gateway as `/jobs`, and it keeps API calls
// from colliding with the SPA's own client routes (e.g. a future `/jobs` page).
const target =
  process.env['services__gateway__https__0'] ||
  process.env['services__gateway__http__0'];

if (!target) {
  // Fail loud in dev rather than silently proxying nowhere — the AppHost is meant to inject this.
  console.warn(
    '[proxy] No gateway address injected (services__gateway__https__0 / __http__0). ' +
      'Run the app via `aspire run`, not `ng serve` directly.',
  );
}

module.exports = [
  {
    context: ['/api'],
    target,
    secure: false, // dev gateway uses a self-signed cert
    changeOrigin: true,
    pathRewrite: { '^/api': '' },
  },
];
