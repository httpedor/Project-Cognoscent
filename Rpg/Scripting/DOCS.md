# Script expressions — reference

## Overview 🎯

Expressions are written in a small typed language (the "DSL"). A data field opts into it by holding
a string that begins with `=`:

```json
{
  "maxStability": "= (80 + target_component.constitution) * 0.5",
  "canEnterPosture": "= array_count(target_component.bps_by_tag('foot')) >= 1",
  "idealHeight": 1
}
```

Anything that is *not* a `=` string is a **literal**: a number, a boolean, a string, `null`, or an
array of those. `"idealHeight": 1` is the constant 1; no expression syntax is involved.

There is no other encoding. The `{"op": "sum", "numbers": [...]}` object form is gone — a field
still written that way reports the call it should now be, e.g. `"= sum(numbers)"`.

---

## How a field is compiled

```
field JSON
    ↓ JsonSource      "= …" → Lexer → Parser        (typed AST, nodes carry line/column)
                      anything else → literal node
AstNode tree
    ↓ AstCompiler     lowering directed by the type the field expects
Expr tree
```

`ExpressionCompiler` (`Compiler.cs`) is a façade over exactly this: its methods differ only in which
`ExprType` they aim at. `AstCompiler` is the only place a node becomes an expression, so a rule —
what a bare number means in a condition, what `null` means in an effect — is written once.

Errors carry a position:

```
DSL error (line 1, col 6): Unknown operation 'notanop'. Did you mean 'not'?
     1 + notAnOp(2)
         ^
```

An unknown op or identifier is reported where it is written. A failed *overload* is reported at the
call, listing each candidate and why it was rejected — a different overload might have accepted the
argument, so the argument itself is not necessarily what is wrong.

---

## Syntax

| Form | Meaning |
| --- | --- |
| `1`, `2.5`, `-3` | numbers |
| `'text'`, `"text"` | strings |
| `true`, `false` | booleans |
| `1d6`, `2d8` | dice |
| `$0`, `$1` | variable slots |
| `target`, `caller`, `target_part` | context symbols |
| `[a, b, c]` | array literal |
| `+ - * /`, `< <= > >=`, `== !=`, `and or not` (`&& \|\| !`) | operators |
| `c ? a : b` | conditional |
| `f(a, b)` | call |
| `x.f(a)` | method sugar — exactly `f(x, a)` |
| `x.name` | reads stat `name` from `x` — exactly `stat('name', x)` |
| `lib::fn(a)` / `x.lib::fn(a)` | call `fn` from expression library `lib` |
| `p => body` | function argument |
| `switch v { … }` | case table (below) |

### `switch`

Case keys are numbers, inclusive `min..max` ranges, or `_` for the fallback:

```
= switch array_count(feet) { 0 => 0.1, 1 => 0.33, _ => 1 }
= switch severity { 0..2 => 'minor', 3..6 => 'serious', _ => 'critical' }
```

All-exact tables compile to a dictionary lookup; any range arm makes the whole table an ordered scan,
so overlapping ranges resolve in the order written.

### Function arguments

`map`, `filter`, `order`, `all`, `any`, `foreach`, `top` and `bottom` take a function. Write it
either way — they mean the same thing:

```
= filter(parts, p => p.bp_health() > 0)
= filter(parts, $0.bp_health() > 0)
```

---

## Variables

- Slots are referenced by index with `$N`.
- `$0` is the innermost binding. Entering a scope shifts existing variables up: inside
  `map(parts, …)` the current element is `$0` and whatever was `$0` outside is now `$1`.
- Bindings come from three places: a function body, `with_vars([…], expr)`, and the arguments of a
  library call. Everything beyond those is the host's own root slots (the body writes injury
  severity into slot 0, and so on).
- A library call starts a **fresh** scope — the called expression sees its arguments as `$0`, `$1`,
  … and cannot see the caller's bindings.
- A lambda parameter name (`p => …`) resolves to its slot, so `p` and `$0` are interchangeable.
- Named aliases for numbered slots (like `"STR"`) are **not** supported.

Scoping is implemented once, in `EvalContext.Push`; combinators never manipulate variable storage
themselves. A function body is a real `LambdaExpr` node, so `map` has the honest type
`(a[], (a) -> b) -> b[]`.

---

## What a literal means

The type a field expects decides how a literal reads. This is the whole table:

| Literal | in number | in condition | in effect | in string | in enum / compendium ref |
| --- | --- | --- | --- | --- | --- |
| `3` | constant | probability (must be 0–1) | — | — | — |
| `"2:5"`, `"1d6"`, `"3-10"` | random in range | — | — | the text | — |
| `"30%"` | — | probability | — | the text | — |
| `true` / `false` | — | constant | `false` = do nothing | — | — |
| `null` | — | false | do nothing | — | no entity / no component |
| `"name"` | must parse as a number | must parse as a bool | — | the text | the named entry |
| `[…]` | — | — | run in order | — | — |

A string naming an enum member or a compendium entry is an *implicit conversion*, so a computed id
works the same as a literal one: `"= concat(['fire_', $0])"` in an `InjuryType` position resolves the
entry at evaluation time.

---

## Declaring an op (for engine developers)

An operation is a static method marked `[ExprOp("name", "alias", …)]` whose **parameters are real,
typed C# parameters**. That single signature is the only declaration: `Tools/ExprGenerator` derives
from it the op's parameter names, their expression types, which are optional, the result type, the
argument binding and the schema.

```csharp
[ExprOp("stat", "creature_stat", "entity_stat", Description = "Reads a named stat from an entity.")]
public static Expr<float> Op(
    [Doc("Name of the stat to read")] string stat,                     // literal: must be constant
    [Doc("Entity to read from; defaults to the caller")] Expr<Entity?>? entity = null,
    [Doc("Value when the stat is absent")] Expr<float>? @default = null)
    => new StatExpr(stat, entity ?? new CallerEntityExpr(), @default ?? new ConstNumberExpr(0));
```

Rules:

- **`Expr<T>` / `ArrayExpr<T>` / `EffectExpr`** parameters receive a compiled expression.
- **Bare `string` / `float` / `int` / `bool` / enum** parameters are *literals*: the value must be a
  compile-time constant, and the op receives the unwrapped value. Say this explicitly rather than
  reading a constant out of an `Expr<T>` at build time.
- **`VariableRef`** accepts a slot index, a `$N`, or a context name (`target`, `caller`).
- **A default value makes a parameter optional**; it arrives as `null` when omitted. A parameter
  with no default is required, and the resolver reports it by name when it is missing.
- The **result type** comes from the return type, so array-ness and category are consequences of the
  signature rather than separate flags.
- Arguments bind **positionally**, in declaration order.

Overloads are selected by type: several ops may share a name (`rand` is both a number and a
condition), and the one whose signature fits the arguments and the surrounding expected type wins.
When nothing matches, the error lists every candidate and why each was rejected.

Polymorphic ops (`if`, `map`, `filter`, …) are registered by hand in `Types/MetaOps.cs`, because
their signatures contain type variables (`map : (a[], a -> b) -> b[]`) and their construction is not
a plain factory call.

### The type system

`Types/ExprType.cs` is the vocabulary: primitives, CLR-backed refs, arrays, functions, type
variables, and `any`. Two things there are worth knowing:

- **`ToClr` vs `ElementClr`.** A scalar `void` is an effect that has already run (`NoReturn`); an
  *array* of `void` is a list of effects still to be run (`EffectExpr`). `composite([…])` depends on
  the difference.
- **`Adapt`** is where an accepted-but-not-identical type becomes a real node: a checked cast, an
  element-wise array cast, or the name→entry conversion above.

### Checking a change

`Tools/ExprCheck` loads the whole `Server/Data` corpus and dumps every compiled expression tree to a
stable text file, then runs smoke tests over the op surface, the literal table, and error positions.
Diff the snapshot before and after a compiler change to see exactly what moved:

```bash
dotnet run --project Tools/ExprCheck -- Server out.snap
```

---

For implementation details, start at `Compiler.cs` and follow it into `Dsl/`.
