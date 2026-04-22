import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

const toolName = "search_gameobjects";
const toolDescription =
  "Searches loaded Unity scenes for GameObjects by name, tag, layer, active state, component type, or parent hierarchy.";

const paramsSchema = z.object({
  name: z.string().optional().describe("Optional GameObject name filter."),
  useRegex: z
    .boolean()
    .optional()
    .describe("Treat the name filter as a regular expression."),
  exactMatch: z
    .boolean()
    .optional()
    .describe("Require an exact name match when useRegex is false."),
  tag: z.string().optional().describe("Optional tag filter."),
  layer: z.number().int().optional().describe("Optional numeric layer filter."),
  layerName: z.string().optional().describe("Optional layer name filter."),
  isActive: z
    .boolean()
    .optional()
    .describe("Optional activeInHierarchy filter."),
  componentType: z
    .string()
    .optional()
    .describe("Optional component type name filter."),
  parentPath: z
    .string()
    .optional()
    .describe("Optional parent GameObject hierarchy path filter."),
  parentId: z
    .number()
    .int()
    .optional()
    .describe("Optional parent GameObject instance ID filter."),
  includeInactive: z
    .boolean()
    .optional()
    .describe("Include inactive objects while traversing the hierarchy."),
  limit: z
    .number()
    .int()
    .min(1)
    .max(500)
    .optional()
    .describe("Maximum number of results to return."),
});

export function registerSearchGameObjectsTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  if (params.parentId !== undefined && params.parentPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Use either 'parentId' or 'parentPath', not both"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      ...params,
      includeInactive: params.includeInactive ?? true,
      limit: params.limit ?? 100,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || "Failed to search GameObjects in Unity"
    );
  }

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(
          {
            message: response.message,
            count: response.count ?? response.results?.length ?? 0,
            results: response.results ?? [],
          },
          null,
          2
        ),
      },
    ],
  };
}