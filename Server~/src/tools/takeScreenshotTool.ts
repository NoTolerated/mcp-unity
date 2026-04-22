import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

const toolName = "take_screenshot";
const toolDescription =
  "Captures the Unity Scene view to a PNG file and returns the saved file path with image metadata.";

const paramsSchema = z.object({
  mode: z
    .enum(["scene"])
    .optional()
    .describe("Screenshot mode. Only 'scene' is supported in this version."),
  width: z
    .number()
    .int()
    .min(32)
    .max(4096)
    .optional()
    .describe("Optional capture width in pixels."),
  height: z
    .number()
    .int()
    .min(32)
    .max(4096)
    .optional()
    .describe("Optional capture height in pixels."),
  filePath: z
    .string()
    .optional()
    .describe("Optional absolute or project-relative output path."),
});

export function registerTakeScreenshotTool(
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
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      mode: params.mode ?? "scene",
      width: params.width,
      height: params.height,
      filePath: params.filePath,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || "Failed to capture a Unity screenshot"
    );
  }

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(response, null, 2),
      },
    ],
  };
}