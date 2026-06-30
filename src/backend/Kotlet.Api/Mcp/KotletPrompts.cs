using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Kotlet.Api.Mcp;

[McpServerPromptType]
public sealed class KotletPrompts
{
    [McpServerPrompt(Name = "create_recipe_flow"),
     Description("Explains how an agent should create a new Kotlet recipe in one shot through MCP.")]
    public static IReadOnlyList<ChatMessage> CreateRecipeFlow() =>
    [
        new(ChatRole.User,
            """
            You can add new Kotlet recipes, but you cannot edit recipes through MCP. Treat recipe creation as a one-shot operation.

            Required flow:
            1. Gather the user's recipe intent, including title, serving count, ingredients, quantities, and any ingredient-specific notes.
            2. Use `get_ingredients` to find existing Kotlet ingredient IDs. If a match is ambiguous or you need measurement details, read `kotlet://ingredients/{ingredientId}`.
            3. Compose `descriptionMarkdown` yourself. It must include a concise description and numbered preparation/cooking steps.
            4. Call `add_recipe` once with a complete `CreateRecipeRequest`:
               - `title`: non-empty recipe title.
               - `servings`: positive serving count.
               - `descriptionMarkdown`: overview plus numbered steps.
               - `ingredients`: each item must include an existing `ingredientId`, positive `quantity`, `unit`, and optional `note`.
            5. If `add_recipe` returns validation errors, explain those errors to the user. Do not call an edit recipe tool; none is exposed.
            """)
    ];
}
