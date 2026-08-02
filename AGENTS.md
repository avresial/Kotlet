## C# readability and formatting

### General principles

* Treat `.editorconfig` as the source of truth for formatting and naming rules.
* Follow conventions already established in the surrounding code.
* Prefer readable, explicit code over terse or clever implementations.
* Keep changes focused. Do not reformat, rename, or refactor unrelated code.
* Use modern C# features when they improve clarity and are supported by the project.
* Do not introduce new abstractions, dependencies, or design patterns without a concrete benefit.

### Readability

* Use descriptive names that communicate intent and domain meaning.
* Avoid abbreviations except established domain terms.
* Keep methods focused on one responsibility.
* Prefer guard clauses and early returns over deeply nested conditions.
* Extract complex conditions into clearly named variables or methods.
* Avoid boolean parameters when separate methods or an enum would express intent better.
* Avoid comments that merely repeat the code. Comments should explain why something exists, important constraints, or non-obvious decisions.
* Keep related code close together.
* Do not create helper methods that make the control flow harder to follow.
* Avoid premature generalization. Extract reusable code when duplication or a clear domain concept exists.
* Respect nullable reference type annotations. Do not use the null-forgiving operator (`!`) unless null safety has been established.
* Preserve existing public APIs unless the task explicitly requires changing them.

### C# conventions

* Use `PascalCase` for types, methods, properties, constants, and public members.
* Prefix interfaces with `I`.
* Use `camelCase` for parameters and local variables.
* Use `_camelCase` for private instance fields.
* Use meaningful boolean names such as `IsValid`, `HasAccess`, or `CanExecute`.
* Add the `Async` suffix to methods returning `Task` or `Task<T>`, except established framework methods.
* Use C# type keywords such as `string`, `int`, and `bool` instead of CLR names.
* Use `var` only when the resulting type is obvious from the expression.
* Use expression-bodied members only for simple, immediately understandable expressions.
* Always use braces for `if`, `else`, loops, and similar control-flow statements.
* Write one statement and one declaration per line.
* Prefer file-scoped namespaces when consistent with the project.
* Place `CancellationToken` last in a method's parameter list.
* Pass cancellation tokens through asynchronous call chains where supported.

### Formatting

* Do not manually override formatting defined in `.editorconfig`.
* Use four spaces for indentation unless `.editorconfig` specifies otherwise.
* Break long argument lists, constructor calls, and fluent chains across multiple lines when this improves readability.
* Keep indentation consistent for multiline expressions.
* Do not align code using additional spaces; automated formatting must remain stable.
* Remove unused `using` directives.
* Ensure every file ends with a newline.
* Do not commit trailing whitespace.
* Do not apply formatting changes to generated files.

### Validation

Before completing a C# change:

1. Run `dotnet format --verify-no-changes`.
2. Run `dotnet build`.
3. Run the relevant tests.
4. Review the diff for unrelated formatting or generated-file changes.

If formatting fails, fix the code or `.editorconfig` violation instead of suppressing the rule.
