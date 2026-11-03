# MCP recipe image upload

Added `upload_recipe_image` for attaching one generated or selected JPEG/PNG/WebP image to an existing
household recipe through a native file reference. The tool returns the stored image ID, content URL,
alt text and metadata. Existing ownership checks, 5 MB input limit, WebP processing and 10-image gallery
limit apply; `update_recipe` behavior is unchanged.

Uploads fail closed until an operator configures verified exact hostnames in
`McpRecipeImages:AllowedDownloadHosts`. Retrieval is restricted to HTTPS port 443 and public destination
addresses, with no redirects/proxies/cookies, a 10-second deadline and bounded streaming reads. No
deployment or production allowlist changes are included.

See [ChatGPT setup and verification](../chatgpt-mcp-setup.md#attach-a-generated-or-selected-recipe-image)
for the file contract, configuration and post-deployment generated-image check. Hosted ChatGPT
generated-file transfer requires validation against the deployed, refreshed connector; local MCP tests
use controlled download responses and do not establish that external capability.
