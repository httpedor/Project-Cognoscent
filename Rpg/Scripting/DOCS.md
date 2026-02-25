# Script expression JSON — Compiler reference

## Overview 🎯
This document describes the JSON formats that `CompileContext` (see `Compiler.cs`) accepts for expressions used throughout the compendium and scripting system.

Supported expression categories:
- `Number` expressions — numeric literals, variables, ranges, arithmetic, stat lookups
- `Condition` expressions — booleans, comparisons, logical combinators, probabilities
- `Effect` expressions — named effects (currently only `noeffect`/`nop` supported)
- `Selector` expressions — resolve an Entity (caller, target, variable-based)
- `String` expressions — literals and variable/argument-based strings
- `CompendiumEntry` expressions — string IDs that reference compendium entries

Use these JSON forms when authoring data in the compendium, skills, or scripting fields.

---

## Symbols & variable names
- Variable names are case-insensitive and are converted to symbol IDs by `CompileContext.GetSymbol`.
- Common attribute aliases are supported (e.g. `STR` &rarr; `STRENGTH`, `HP` &rarr; `HEALTH`).
- Refer to variable names directly as string values in `Number` or `Condition` expressions (e.g. `"HP"`).

---

## Number expressions (Expr<float>) 🔢
Accepts:
- A JSON number => constant (e.g. `3.5`)
- A string => symbol name or dice/range shorthand (e.g. `"STR"`, `"1d6"`, `"2-5"`)
- An object with an `op` property for composite operations

Common `op` object forms:
- lerp: { "op": "lerp", "min": <Expr<float>>, "max": <Expr<float>>, "t": <Expr<float>> }
- rand / random / range: { "op": "rand", "min": <Expr<float>>, "max": <Expr<float>> }
- stat / creature_stat: { "op": "stat", "stat": "STATNAME", "entity": <Expr<Entity?>>?, "default": <Expr<float>>? }
- sum / plus / add / +: { "op": "sum", "numbers": [<Expr<float>>...] }
- sub / minus / -: { "op": "sub", "numbers": [<Expr<float>>...] }
- mul / *: { "op": "mul", "numbers": [<Expr<float>>...] }
- div / /: { "op": "div", "numbers": [<Expr<float>>...] }

Shorthand/random notation:
- Dice-like and range strings are recognized (e.g. `"1d6"`, `"2-5"`, `"3:6"`).

Examples:
- 5
- "STR"
- "1d8"
- { "op": "sum", "numbers": ["STR", 2, {"op":"rand","min":1,"max":4}] }

---

## Condition expressions (Expr<bool>) ✅/❌
Accepts:
- A JSON boolean `true`/`false`
- A number between 0 and 1 (probability), or a percent string like `"30%"`
- A symbol name string (treated as a boolean variable)
- An object with `op` describing logical/comparison operators

Supported `op` values:
- Logical: `and`, `or`, `not` (for `and`/`or` provide `conditions` array)
- Comparisons: `>`, `>=`, `<`, `<=`, `==` or `=`, `!=` — use `left` and `right` (both Expr<float>)
- `var` — { "op": "var", "name": "VAR_NAME" }
- `random`/`rand` — { "op": "random", "probability": <number> }

Examples:
- true
- "50%"
- "HAS_KEY"
- { "op":"and", "conditions": ["HAS_KEY", {"op":">", "left":"STR", "right":5}] }

---

## Effect expressions (EffectExpr) ✨
- Currently only named effects are accepted as objects with an `effect` property.
- The compiler recognizes `noeffect`, `nop`, `null` (returns a `NoEffectExpr`).
- Additional effects may be supported later by extending `CompileEffectObj`.

Example: { "effect": "noeffect" }

---

## Selector expressions (Expr<Entity?>) 🧭
Used by `stat` lookups and other places that need to identify an Entity.
Accepts:
- Strings: `"self"`, `"caller"`, `"target"`, `"target_part"`, or a variable name
- Objects: { "selector": "caller" } (same semantics)

When a variable name is used the symbol is resolved to an entity at runtime.

Examples:
- "caller"
- { "selector": "target" }
- "attacker" (a symbol resolved to an Entity)

---

## String expressions (StringExpr) 📝
- A plain JSON string is a literal, unless it starts with `$` — then it references a symbol (e.g. `"$name"`).
- Object forms support operations such as retrieving argument type names: `{ "op": "argumentType", "argument": "argName" }`.

Examples:
- "Hello, world"
- "$character_name"
- { "op": "argumentType", "argument": "weapon" }

---

## Compendium entry expressions
- Expect a `StringExpr` containing the entry ID (typically a literal string or `$var`).
- Example: `"monster_goblin"` or `"$selected_monster"`.

---

## Error handling & limits
- Missing or unknown `op` values raise compile exceptions.
- Probability numbers > 1 are interpreted as percentages in some places; prefer `0-1` or `"N%"` for clarity.

---

## Quick examples
- Number: { "op":"sum", "numbers": ["STR", {"op":"rand","min":1,"max":4}] }
- Condition: { "op":"and", "conditions": ["ALIVE", {"op":">","left":"HP","right":0}] }
- Stat lookup: { "op":"stat", "stat":"HEALTH", "entity":"target" }
- Selector variable: "attacker"

---

For implementation details, consult `Rpg/Scripting/Compiler.cs`.

> Note: This document mirrors the JSON forms accepted by `CompileContext`. If you add new expression kinds, update this doc and `schema.json` together. 💡
