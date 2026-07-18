# Adding Seed Data

How to add development seed data so the demo databases come up populated and every developer sees the
same content after `aspire run`. Grounded in the real seeders (`ProfilesSeedData`, `IdentitySeedData`,
`JobsSeedData`, `ApplicationsSeedData`).

## What seed data is (and isn't) here

Seed data is **development-only demo content** — a couple of accounts, a job or two, a profile — so a
reviewer can open the app and see something real without registering or clicking through empty screens.
It is not fixtures for tests (integration tests manage their own schema and data) and it is not a way to
move data between services.

Each service **owns and seeds only its own database.** There is no shared seeder and no shared table.
When two services need to line up on the same entity — the seeded employer account exists in
`identitydb`, and its company profile in `profilesdb` — they agree by **duplicating a well-known id as a
literal**, never by a cross-service foreign key. That's sanctioned reference-data duplication, the same
rule as everywhere else in the system.

## Where it lives and how it runs

Every service keeps its seeder in `JobBoard.<Service>.Core/Seeding/<Service>SeedData.cs` — a static
class with an idempotent `SeedAsync(...)`. The host runs it once at startup, **Development-only**, right
after applying migrations, in [`src/JobBoard.<Service>/Program.cs`](../src):

```csharp
// Development convenience: migrate the freshly-provisioned db, then seed demo content.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();
    await db.Database.MigrateAsync();

    await JobBoard.Profiles.Core.Seeding.ProfilesSeedData.SeedAsync(db);
}
```

Because it's guarded to Development and runs after `MigrateAsync()`, `aspire run` stays a single command:
the container is provisioned, the schema is applied, and the demo rows land — every time, for every
developer, without a manual step.

## Add or extend a seeder

1. **Pick the owning service.** Seed data goes in the database that owns the entity — a job in
   `JobsSeedData`, a candidate profile in `ProfilesSeedData`. If you're tempted to seed another service's
   table, stop: duplicate the id and seed your own.

2. **Write against the Domain entity.** Seeders use the `.Core` Domain types and the service's
   `DbContext` directly (they're inside `.Core`, so this is fine — no facade/business layering for demo
   data). Populate every required column so the row is valid.

3. **Keep it idempotent — and guard per row, not per table.** Guard on an existence check so a restart
   against a persisted volume doesn't duplicate or throw. Guard on the **specific row's key**, never on
   "does the table have anything". A whole-table `AnyAsync()` gate no-ops the entire seeder the moment one
   row exists, so a *newly-added* seed block never lands until the volume is wiped — the per-row guard is
   what lets you add demo data without destroying data. The shapes in use:

   ```csharp
   // Per-id guard — seed a well-known row only if that id is absent:
   if (!await db.Accounts.AnyAsync(a => a.Id == EmployerId, cancellationToken))
       db.Accounts.Add(new Account { Id = EmployerId, ... });

   // Natural-key guard — when the row's identity is a business key, not a seeded literal id
   // (an application is one candidate applying to one job): seed only the missing pairs.
   var existing = (await db.Applications
           .Where(a => a.CandidateId == CandidateId).Select(a => a.JobId).ToListAsync(cancellationToken))
       .ToHashSet();
   db.Applications.AddRange(seed.Where(a => !existing.Contains(a.JobId)));
   ```

   Always finish with a single `await db.SaveChangesAsync(cancellationToken)`.

   **Shared child rows (Jobs' categories/tags):** when new rows reference de-duplicated data shared across
   seeds — a `Category`/`Tag` keyed by slug — pre-load what's already in the db so a new row reuses the
   existing child instead of inserting a duplicate (the seeder writes directly, bypassing the repository's
   own slug reconciliation):

   ```csharp
   // Pool keyed by slug, primed from the db; category(slug)/tag(slug) return the existing row or a new one.
   var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, cancellationToken);
   var tags = await db.Tags.ToDictionaryAsync(t => t.Slug, cancellationToken);
   ```

4. **Reuse well-known ids for cross-service alignment.** If your row corresponds to a seeded entity in
   another service, use the same literal `Guid`. The canonical ids are the demo accounts, defined by
   literal in every seeder:

   ```csharp
   public static readonly Guid EmployerId  = new("e0000000-0000-0000-0000-000000000001");
   public static readonly Guid CandidateId = new("c0000000-0000-0000-0000-000000000001");
   ```

   Keep the literals identical across seeders — that's the contract. `IdentitySeedData` also owns the
   shared demo credentials (`DemoPassword`, `EmployerEmail`, `CandidateEmail`) shown on the sign-in hint.

5. **Need a service, not just the `DbContext`?** Resolve it from the same scope and pass it in — the way
   `IdentitySeedData` takes an `IPasswordHasher` to hash the demo password:

   ```csharp
   var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
   var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
   await JobBoard.Identity.Core.Seeding.IdentitySeedData.SeedAsync(db, hasher);
   ```

6. **New service? Wire the seeder in `Program.cs`.** Add the `MigrateAsync()` + `SeedAsync(...)` block
   inside the `IsDevelopment()` guard, after `builder.Build()`. Existing services already have it.

## Applying your changes

Seeders run at host startup, so:

- **First run / empty database:** `aspire run` provisions the container, migrates, and seeds. Done.
- **You *added* seed rows (a new account, job, profile, application):** just restart `aspire run`. The
  per-row guards seed only the missing rows, so the new content lands against the existing volume and
  nothing already there is touched — **no wipe.** This is the whole point of the per-row guards; adding
  test data never requires destroying data.
- **You *edited or removed* an existing seeded row:** the guard sees the row's key already present and
  leaves it as-is, so an in-place change to a row you already seeded won't re-apply. That's deliberate —
  it protects data you (or a reviewer) changed by clicking around. To force the canonical rows to refresh,
  wipe just that service's database: stop `aspire run`, remove the service's volume (or the Postgres
  container), and start again; the Postgres data lives in the Aspire-managed container volume.
- **You added a new column:** add and apply a **migration** first (see
  [Adding an Endpoint by Hand](./adding-an-endpoint-manually.md) and `CLAUDE.md` for the EF workflow) —
  seeding does not alter schema.

## Rules to respect

- **Development-only.** Never let a seeder run outside the `IsDevelopment()` guard — no demo accounts in
  a real environment.
- **Own your database.** Seed only the database your service owns; align across services by duplicating a
  literal id, never a foreign key or a second connection string.
- **Idempotent per row, always.** A seeder must be safe to run on every startup, and must guard on each
  row's own key (well-known id or natural key) — never on a whole-table `AnyAsync()`. Per-row guards are
  what let a developer add seed data without wiping the volume.
- **No secrets, no real data.** Seed content is demo content — the shared demo password is fine precisely
  because it's development-only and visible in the UI hint.
