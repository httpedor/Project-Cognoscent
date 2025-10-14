# Copilot Instructions for Project-Cognoscent

## Project Overview
- **Project-Cognoscent** is a TTRPG tool/game using a custom RPG system (Cognoscent) with both a Godot-based client and a .NET CLI server.
- The solution is split into three main components:
  - `Client/` (Godot, C#): Game UI and player interaction.
  - `Server/` (.NET CLI): Game logic, networking, and command-line interface.
  - `Rpg/` (.NET library): Core RPG logic, data models, and system rules.

## Architecture & Data Flow
- The **Client** communicates with the **Server** for game state, actions, and updates. The **Rpg** library is shared between Client and Server for consistent rules and data.
- Combat is tick-based, not turn-based. Actions (strike, weave, parry, etc.) are resolved by ticks, dexterity, and other stats.
- Health is modeled per body part (RimWorld-inspired), not as a single health bar.

## Build & Run Workflows
- **Build all projects:**
  - Use the VS Solution: `dotnet build TTRpgClient.sln`
  - Or use the VS Code build task: `build`
- **Run Client:**
  - Requires Godot Engine (see `Client/project.godot`).
  - Open in Godot and run, or use Godot CLI.
- **Run Server:**
  - CLI tool, run with `dotnet run --project Server/Server.csproj`.
  - Type `help` in the server CLI for available commands.
- **Watch mode:**
  - Use the VS Code task: `watch` to auto-rebuild and run the solution.

## Key Conventions & Patterns
- **Tick-based combat:** See `Rpg/Features/`, `Rpg/Health/`, and `Client/scenes/` for implementation.
- **Body part health:** See `Rpg/Health/Body.cs`, `Rpg/Health/BodyPart.cs`.
- **Actions and timing:** Actions are modeled as classes with tick delays and recovery. Example: `Strike`, `Weave`, `Parry`.
- **Shared logic:** All game rules and models are in `Rpg/`, referenced by both Client and Server.
- **Nullable and preview C# features:** All projects use `<Nullable>enable</Nullable>` and `<LangVersion>preview</LangVersion>`.

## External Dependencies
- **Godot.NET.Sdk** for Client (see `Client/TTRpgClient.csproj`).
- **Microsoft.CodeAnalysis.CSharp.Scripting** and **System.Drawing.Common** for Rpg.

## Examples
- To add a new combat action, implement in `Rpg/Skills/` and expose via Client UI and Server CLI.
- To change health logic, update `Rpg/Health/Body.cs` and related files.

## Tips for AI Agents
- Always reference shared logic in `Rpg/` for consistency.
- Use tick-based timing for all combat and action logic.
- Prefer Godot for UI/gameplay, .NET CLI for server/game logic.
- Use provided build and watch tasks for development.

---