using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Kotlet.Api.Mcp;

/// <summary>
/// Keeps agent-visible tool results small while preserving complete data for the MCP App.
/// </summary>
internal static class McpToolResultAdapter
{
    public const string UiDataKey = "kotlet/uiData";

    private static readonly HashSet<string> CompactTools =
    [
        "get_ingredients",
        "create_ingredient",
        "get_meal_plan_members",
        "get_meal_plan",
        "add_weekly_meal_plan",
        "set_meal_participants"
    ];

    public static void Apply(string toolName, CallToolResult result)
    {
        if (result.StructuredContent is not { } fullResult || result.IsError is true)
            return;

        if (!CompactTools.Contains(toolName))
        {
            result.Content = [];
            return;
        }

        var fullData = UnwrapArrayResult(JsonNode.Parse(fullResult.GetRawText()));
        result.Meta ??= [];
        result.Meta[UiDataKey] = fullData?.DeepClone();

        var compact = Compact(toolName, fullData);
        result.StructuredContent = JsonSerializer.SerializeToElement(compact, JsonSerializerOptions.Web);
        result.Content =
        [
            new TextContentBlock { Text = Summary(toolName, compact) }
        ];
    }

    public static JsonElement? OutputSchema(string toolName) => toolName switch
    {
        "get_ingredients" => Schema(
            """
            {"type":"object","properties":{"matches":{"type":"array","items":{"type":"object","properties":{"inputName":{"type":"string"},"ingredientId":{"type":["string","null"],"format":"uuid"},"matchedName":{"type":["string","null"]},"measurementUnit":{"type":["string","null"]},"exactMatch":{"type":"boolean"},"similarity":{"type":["number","null"]},"resourceUri":{"type":["string","null"]}},"required":["inputName","ingredientId","matchedName","measurementUnit","exactMatch","similarity","resourceUri"],"additionalProperties":false}}},"required":["matches"],"additionalProperties":false}
            """),
        "create_ingredient" => OperationSchema(
            """
            "ingredientId":{"type":["string","null"],"format":"uuid"},"name":{"type":["string","null"]},"measurementUnit":{"type":["string","null"]},"resourceUri":{"type":["string","null"]}
            """),
        "get_meal_plan_members" => Schema(
            """
            {"type":"object","properties":{"members":{"type":"array","items":{"type":"object","properties":{"userId":{"type":"string","format":"uuid"},"displayName":{"type":"string"}},"required":["userId","displayName"],"additionalProperties":false}}},"required":["members"],"additionalProperties":false}
            """),
        "get_meal_plan" => Schema(
            """
            {"type":"object","properties":{"days":{"type":"array","items":{"type":"object","properties":{"date":{"type":"string"},"meals":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"slot":{"type":"string"},"type":{"type":"string"},"recipeId":{"type":["string","null"],"format":"uuid"},"ingredientId":{"type":["string","null"],"format":"uuid"},"preparedMealId":{"type":["string","null"],"format":"uuid"},"displayName":{"type":"string"},"note":{"type":["string","null"]},"participants":{"type":"array","items":{"type":"object","properties":{"userId":{"type":"string","format":"uuid"},"displayName":{"type":"string"},"portionPercent":{"type":"integer"}},"required":["userId","displayName","portionPercent"],"additionalProperties":false}},"guests":{"type":"integer"},"servings":{"type":"number"}},"required":["id","slot","type","recipeId","ingredientId","preparedMealId","displayName","note","participants","guests","servings"],"additionalProperties":false}}},"required":["date","meals"],"additionalProperties":false}}},"required":["days"],"additionalProperties":false}
            """),
        "add_weekly_meal_plan" => OperationSchema(
            """
            "addedCount":{"type":"integer"},"skippedCount":{"type":"integer"},"mealIds":{"type":"array","items":{"type":"string","format":"uuid"}}
            """),
        "set_meal_participants" => OperationSchema(
            """
            "mealId":{"type":["string","null"],"format":"uuid"},"participantCount":{"type":"integer"},"servings":{"type":["number","null"]}
            """),
        _ => null
    };

    private static JsonNode Compact(string toolName, JsonNode? fullData) => toolName switch
    {
        "get_ingredients" => new JsonObject
        {
            ["matches"] = MapArray(fullData, Match)
        },
        "create_ingredient" => CompactIngredientOperation(fullData),
        "get_meal_plan_members" => new JsonObject
        {
            ["members"] = MapArray(fullData, member => Pick(member, "userId", "displayName"))
        },
        "get_meal_plan" => new JsonObject
        {
            ["days"] = MapArray(fullData, CompactDay)
        },
        "add_weekly_meal_plan" => CompactWeeklyOperation(fullData),
        "set_meal_participants" => CompactParticipantOperation(fullData),
        _ => new JsonObject()
    };

    private static JsonObject Match(JsonNode? match) =>
        Pick(match, "inputName", "ingredientId", "matchedName", "measurementUnit",
            "exactMatch", "similarity", "resourceUri");

    private static JsonObject CompactIngredientOperation(JsonNode? root)
    {
        var ingredient = root?["ingredient"];
        return Operation(root,
            ("ingredientId", ingredient?["id"]),
            ("name", ingredient?["name"]),
            ("measurementUnit", ingredient?["measurementUnit"]),
            ("resourceUri", ingredient?["id"] is { } id ? $"kotlet://ingredients/{id.GetValue<string>()}" : null));
    }

    private static JsonObject CompactWeeklyOperation(JsonNode? root)
    {
        var added = root?["plan"]?["added"] as JsonArray;
        return Operation(root,
            ("addedCount", added?.Count ?? 0),
            ("skippedCount", root?["plan"]?["skipped"]),
            ("mealIds", new JsonArray(added?
                .Select(item => item?["id"]?.DeepClone())
                .Where(id => id is not null)
                .ToArray() ?? [])));
    }

    private static JsonObject CompactParticipantOperation(JsonNode? root)
    {
        var item = root?["item"];
        return Operation(root,
            ("mealId", item?["id"]),
            ("participantCount", (item?["participants"] as JsonArray)?.Count ?? 0),
            ("servings", item?["servings"]));
    }

    private static JsonObject Operation(JsonNode? root, params (string Name, object? Value)[] fields)
    {
        var result = new JsonObject
        {
            ["status"] = root?["status"]?.DeepClone()
        };
        foreach (var (name, value) in fields)
            result[name] = Node(value);
        if (root?["validationErrors"] is { } validationErrors)
            result["validationErrors"] = validationErrors.DeepClone();
        if (root?["message"] is { } message)
            result["message"] = message.DeepClone();
        return result;
    }

    private static JsonObject CompactDay(JsonNode? day)
    {
        var meals = new JsonArray();
        if (day?["meals"] is JsonObject slots)
        {
            foreach (var (_, slotMeals) in slots)
            {
                foreach (var meal in slotMeals as JsonArray ?? [])
                {
                    var compactMeal = Pick(meal, "id", "slot", "type", "recipeId", "ingredientId",
                        "preparedMealId", "displayName", "note", "guests", "servings");
                    compactMeal["participants"] = MapArray(meal?["participants"],
                        participant => Pick(participant, "userId", "displayName", "portionPercent"));
                    meals.Add(compactMeal);
                }
            }
        }
        return new JsonObject
        {
            ["date"] = day?["date"]?.DeepClone(),
            ["meals"] = meals
        };
    }

    private static JsonObject Pick(JsonNode? source, params string[] names)
    {
        var result = new JsonObject();
        foreach (var name in names)
            result[name] = source?[name]?.DeepClone();
        return result;
    }

    private static JsonArray MapArray(JsonNode? source, Func<JsonNode?, JsonNode?> map) =>
        new((source as JsonArray)?.Select(map).ToArray() ?? []);

    private static JsonNode? UnwrapArrayResult(JsonNode? source) =>
        source is JsonObject { Count: 1 } wrapper && wrapper["result"] is JsonArray result
            ? result.DeepClone()
            : source;

    private static JsonNode? Node(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        _ => JsonValue.Create(value)
    };

    private static string Summary(string toolName, JsonNode compact) => toolName switch
    {
        "get_ingredients" =>
            $"Matched {(compact["matches"] as JsonArray)?.Count ?? 0} ingredient name(s).",
        "create_ingredient" =>
            compact["status"]?.GetValue<string>() == "Success"
                ? $"Created ingredient \"{compact["name"]}\"."
                : $"Ingredient creation returned {compact["status"]}.",
        "get_meal_plan_members" =>
            $"Found {(compact["members"] as JsonArray)?.Count ?? 0} household member(s).",
        "get_meal_plan" =>
            $"Retrieved {(compact["days"] as JsonArray)?.Count ?? 0} meal-plan day(s).",
        "add_weekly_meal_plan" =>
            $"Added {compact["addedCount"] ?? 0} meal(s); skipped {compact["skippedCount"] ?? 0}.",
        "set_meal_participants" =>
            compact["status"]?.GetValue<string>() == "Success"
                ? $"Updated meal participants ({compact["participantCount"] ?? 0} assigned)."
                : $"Updating meal participants returned {compact["status"]}.",
        _ => "Kotlet tool completed."
    };

    private static JsonElement OperationSchema(string fields) => Schema(
        """{"type":"object","properties":{"status":{"type":"string"},"""
        + fields
        + ""","validationErrors":{"type":["object","null"],"additionalProperties":{"type":"array","items":{"type":"string"}}},"message":{"type":["string","null"]}},"required":["status"],"additionalProperties":false}""");

    private static JsonElement Schema(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}
