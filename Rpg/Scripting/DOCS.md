# Script expression JSON — Compiler reference

## Overview 🎯
This document describes the JSON formats that `ExpressionCompiler` (see `Compiler.cs`) accepts for expressions used throughout the compendium and scripting system.

Supported expression categories:
- `Number` expressions — numeric literals, indexed variables, ranges, arithmetic, stat lookups
- `Condition` expressions — booleans, comparisons, logical combinators, probabilities
- `Effect` expressions — named effects (currently only `noeffect`/`nop` supported)
- `Selector` expressions — resolve an Entity (caller, target, variable-based)
- `String` expressions — literals, indexed variables, and concatenation
- `CompendiumEntry` expressions — string IDs that reference compendium entries

Use these JSON forms when authoring data in the compendium, skills, or scripting fields.

---

## Variables
- Variables are referenced by integer index using the `$N` syntax (e.g. `"$0"`, `"$1"`).
- The `EvalContext.Variables` array holds variable values at runtime.
- Named symbol aliases (like `"STR"` or `"HP"`) are **not** supported — use `$N` indices instead.

---

## Conditional `if` expression (all types)
Any expression type supports an inline conditional by using `"op": "if"`:

```json
{ "op": "if", "condition": <Expr<bool>>, "true": <Expr<T>>, "false": <Expr<T>> }
```

This works for Number, Condition, Selector, and String expressions.

---

## Number expressions (`Expr<float>`) 🔢
Accepts:
- A JSON number — constant (e.g. `3.5`)
- A string — dice/range shorthand or indexed variable:
  - Dice/range: `"1d6"`, `"2-5"`, `"3:6"`, `"1D8"` (any string containing `d`, `D`, `-`, `:`, or `,` with length > 2)
  - Variable: `"$N"` where N is an integer index into `EvalContext.Variables`
- An object with an `op` property for composite operations

`op` object forms:
- `lerp`: `{ "op": "lerp", "min": <Expr<float>>, "max": <Expr<float>>, "t": <Expr<float>> }`
- `rand` / `random` / `range`: `{ "op": "rand", "min": <Expr<float>>, "max": <Expr<float>> }`
- `stat` / `creature_stat` / `entity_stat`: `{ "op": "stat", "stat": "STATNAME", "entity": <Expr<Entity?>>?, "default": <Expr<float>>? }` — `entity` defaults to caller, `default` defaults to 0
- `sum` / `plus` / `add` / `addition` / `+`: `{ "op": "sum", "numbers": [<Expr<float>>...] }`
- `sub` / `subtract` / `minus` / `subtraction` / `-`: `{ "op": "sub", "numbers": [<Expr<float>>...] }`
- `mul` / `multiply` / `times` / `multiplication` / `*`: `{ "op": "mul", "numbers": [<Expr<float>>...] }`
- `div` / `divide` / `division` / `/`: `{ "op": "div", "numbers": [<Expr<float>>...] }`

Examples:
```json
5
"1d8"
"$0"
{ "op": "sum", "numbers": ["$0", 2, { "op": "rand", "min": 1, "max": 4 }] }
{ "op": "stat", "stat": "HEALTH", "entity": "target", "default": 0 }
```

---

## Condition expressions (`Expr<bool>`) ✅/❌
Accepts:
- JSON `true` / `false`
- `null` — treated as `false`
- A number: between 0–1 is used as probability directly; values > 1 are divided by 100 (treated as percentage)
- A string:
  - `"true"` / `"false"` — constant
  - `"N%"` — probability (e.g. `"30%"`)
  - `"$N"` — indexed boolean variable
- An object with an `op` property

`op` object forms:
- `and`: `{ "op": "and", "conditions": [<Expr<bool>>...] }`
- `or`: `{ "op": "or", "conditions": [<Expr<bool>>...] }`
- `not`: `{ "op": "not", "condition": <Expr<bool>> }`
- `true` / `false`: constant boolean
- `>`: `{ "op": ">", "left": <Expr<float>>, "right": <Expr<float>> }`
- `>=`: `{ "op": ">=", "left": <Expr<float>>, "right": <Expr<float>> }` (implemented as `not <`)
- `<`: `{ "op": "<", "left": <Expr<float>>, "right": <Expr<float>> }`
- `<=`: `{ "op": "<=", "left": <Expr<float>>, "right": <Expr<float>> }` (implemented as `not >`)
- `=` / `==`: `{ "op": "==", "left": <Expr<float>>, "right": <Expr<float>> }`
- `!=`: `{ "op": "!=", "left": <Expr<float>>, "right": <Expr<float>> }`
- `random` / `rand`: `{ "op": "random", "probability": <number> }` — defaults to 0.5

Examples:
```json
true
0.3
"50%"
"$0"
{ "op": "and", "conditions": ["$0", { "op": ">", "left": "$1", "right": 5 }] }
{ "op": "random", "probability": 0.25 }
```

---

## Effect expressions (`EffectExpr`) ✨
Accepts an object with an `effect` property, or `false`/`null` for no effect.

Recognized `effect` values:
- `null` / `nop` / `noeffect` / `no_effect` — returns `NoEffectExpr`

Additional effects may be added by extending `CompileEffectObj`.

Examples:
```json
{ "effect": "noeffect" }
false
null
```

---

## Selector expressions (`Expr<Entity?>`) 🧭
Used by `stat` lookups and other places that need to identify an Entity.
Accepts:
- Strings:
  - `"self"` / `"caller"` — the entity executing the script
  - `"target"` — the target entity
  - `"target_part"` — the exact body part of the target
  - `"$N"` — indexed entity variable
- Objects with an `op` property:
  - `{ "op": "caller" }` / `{ "op": "self" }`
  - `{ "op": "target" }`
  - `{ "op": "target_part" }`
- `null` / `false` — resolves to no entity

Examples:
```json
"caller"
"target"
"target_part"
"$0"
{ "op": "target" }
```

---

## String expressions (`Expr<string>`) 📝
Accepts:
- A plain JSON string — literal value, unless it starts with `$N` (integer variable index)
- An object with an `op` property

`op` object forms:
- `concat` / `add` / `join`: `{ "op": "concat", "strings": [<Expr<string>>...] }`

Examples:
```json
"Hello, world"
"$0"
{ "op": "concat", "strings": ["Hello, ", "$0", "!"] }
```

---

## Compendium entry expressions
- Expects a `StringExpr` containing the entry ID.
- Typically a literal string or an indexed variable.

Examples:
```json
"monster_goblin"
"$0"
```

---

## Enum expressions
- Expects a `StringExpr` whose runtime value matches an enum member name.
- Parsed case-insensitively at evaluation time.

---

## Error handling & limits
- Missing or unknown `op` values raise compile-time exceptions.
- Probability numbers: prefer `0`–`1` range or `"N%"` strings for clarity; bare numbers > 1 are treated as percentages (divided by 100).
- `run_script` / `invoke_script` ops in number expressions are explicitly not supported — script invocation must be handled outside the expression system.

---

## Quick examples
```json
// Number: sum of a variable and a dice roll
{ "op": "sum", "numbers": ["$0", { "op": "rand", "min": 1, "max": 4 }] }

// Condition: variable is true AND another variable > 0
{ "op": "and", "conditions": ["$0", { "op": ">", "left": "$1", "right": 0 }] }

// Stat lookup on the target entity
{ "op": "stat", "stat": "HEALTH", "entity": "target" }

// Inline conditional number
{ "op": "if", "condition": "$0", "true": 10, "false": 0 }

// String concat
{ "op": "concat", "strings": ["Damage: ", "$0"] }
```

---

For implementation details, consult `Rpg/Scripting/Compiler.cs`.

> Note: This document mirrors the JSON forms accepted by `ExpressionCompiler`. If you add new expression kinds, update this doc accordingly. 💡
