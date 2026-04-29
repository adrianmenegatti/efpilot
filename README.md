# EfPilot 🚀

A smarter CLI for managing Entity Framework Core migrations.

EfPilot helps you **detect DbContexts, infer startup projects, avoid empty migrations, preview changes, and inspect migration status** — all with a clean, developer-friendly experience.

---

## ✨ Features

* 🔍 **Auto-detect DbContexts**

    * Scans your solution and finds all DbContext classes

* 🧠 **Startup project inference**

    * Automatically determines the correct startup project for each DbContext

* 🚫 **Avoid empty migrations**

    * Detects and removes migrations with no model changes

* 🔎 **Migration diff preview**

    * See what will change before applying migrations

* 📊 **Migration status**

    * Shows applied vs pending migrations in a clean table

* 🎯 **Profiles**

    * Manage multiple DbContexts in a structured way

---

## 📦 Installation

```bash
git clone https://github.com/yourusername/efpilot.git
cd efpilot
dotnet build
```

---

## 🚀 Getting Started

### Initialize configuration

```bash
dotnet run --project src/EfPilot.Cli -- init
```

EfPilot will:

* detect your solution
* find DbContexts
* infer startup projects
* generate a config file

---

### Check migration status

```bash
dotnet run --project src/EfPilot.Cli -- status --all
```

Example output:

```
──────────── MyProfile ── ✔ Applied: 5 | ⏳ Pending: 2 ────────────

Profile: MyProfile
DbContext: MyDbContext

┌───────────────────────────────────────────────┬────────────┐
│ Migration                                     │  Status    │
├───────────────────────────────────────────────┼────────────┤
│ 20260415145507_InitialCreate                  │ ✔ Applied  │
│ 20260416161708_AddImportRunsAndLoans          │ ✔ Applied  │
│ 20260420134851_AddCalculationWorkerEntities   │ ✔ Applied  │
│ 20260421155040_MovedParametersToImportRun     │ ✔ Applied  │
│ 20260428154005_UpdateCalculationJobs          │ ✔ Applied  │
│ 20260430120000_AddNewField                    │ ⏳ Pending │
└───────────────────────────────────────────────┴────────────┘
```

---

### Add a migration

```bash
dotnet run --project src/EfPilot.Cli -- add --profile MyProfile --name AddNewField
```

* Automatically uses correct startup project
* Removes migration if no changes detected

---

### Remove last migration

```bash
dotnet run --project src/EfPilot.Cli -- remove --profile MyProfile
```

---

### Update database

```bash
dotnet run --project src/EfPilot.Cli -- update --profile MyProfile
```

---

### Preview changes (diff)

```bash
dotnet run --project src/EfPilot.Cli -- diff --profile MyProfile
```

Example:

```
✔ Add column 'Code' to 'Loans'
✔ Create index 'IX_Loans_Code'
```

---

## ⚙️ Configuration

EfPilot generates a config file:

```json
{
  "version": 1,
  "solution": "MySolution.slnx",
  "profiles": [
    {
      "name": "MyProfile",
      "dbContext": "MyDbContext",
      "project": "apps/MyApp/infrastructure/MyApp.Infrastructure.csproj",
      "startupProject": "apps/mayapp/api/MyApp.Api.csproj",
      "migrationsFolder": null
    }
  ]
}
```

---

## 🧩 Why EfPilot?

Working with EF Core migrations in multi-project solutions can be painful:

* ❌ You must remember startup project paths
* ❌ Empty migrations pollute your repo
* ❌ No easy way to preview changes
* ❌ Status visibility is limited

EfPilot solves all of this with a simple CLI.

---

## 🛠 Built with

* .NET 10
* Entity Framework Core
* Spectre.Console

---

## 📌 Roadmap

* [ ] Better diff engine
* [ ] Migration history visualization
* [ ] Interactive mode
* [ ] NuGet global tool

---

## 🤝 Contributing

PRs and feedback are welcome!

---

## 📄 License

MIT
