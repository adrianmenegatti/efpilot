# efpilot 🚀

> Stop writing long `dotnet ef` commands in real-world .NET solutions.

**efpilot** is a developer-first tool that simplifies working with Entity Framework Core migrations — especially in **multi-project, multi-context solutions**.

Designed for developers using **Rider on Mac/Linux**, but works anywhere `.NET` runs.

---

## 🤯 The problem

If you’ve worked with EF Core in real projects, you’ve probably done this:

```bash
dotnet ef migrations add AddLoans \
  --project apps/raterisk/infrastructure/RateRisk.Infrastructure \
  --startup-project apps/raterisk/api/RateRisk.Api
```

Now imagine:

* multiple `DbContext`s
* multiple projects
* different environments
* long paths you never remember

👉 It gets painful fast.

---

## 💡 The idea

`efpilot` removes that friction.

Instead of remembering commands, paths and flags…

```bash
efpilot add AddLoans
```

That’s it.

---

## ✨ Features (current)

* 🔍 Auto-detect solution, projects and DbContexts
* ➕ Add migrations without remembering CLI flags
* 📋 List migrations
* ⬆️ Update database
* ❌ Remove last migration
* 🧠 Multi-context support (basic)

---

## 🚧 Roadmap

efpilot is just getting started. Planned improvements include:

* ⚠️ Migration safety analysis (detect destructive changes)
* 🔄 Environment-aware execution (dev / staging / prod)
* 📄 SQL script generation
* 🧭 Interactive TUI mode
* 🏗️ Better support for complex Clean Architecture setups

Have ideas? Open an issue 👇

---

## 🚀 Getting started

> (Coming soon)

For now, clone the repo and run:

```bash
dotnet run --project src/EfPilot.Cli
```

---

## 🎯 Who is this for?

* .NET developers working with EF Core
* People using Rider on Mac/Linux
* Projects with multiple layers (API, Infrastructure, etc.)
* Anyone tired of remembering `dotnet ef` commands 😄

---

## 🤝 Contributing

Feedback, issues and ideas are very welcome.

If this tool saves you time, let me know — that’s the goal.

---

## 📄 License

MIT
