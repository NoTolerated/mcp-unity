import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

const toolName = "manage_asset";
const toolDescription =
  "Creates folders and renames, moves, duplicates, or deletes Unity assets through the AssetDatabase.";

const paramsSchema = z.object({
  action: z
    .enum(["create_folder", "rename", "move", "duplicate", "delete"])
    .describe("The asset operation to perform."),
  assetPath: z
    .string()
    .optional()
    .describe("The source asset path for rename, move, duplicate, or delete."),
  parentFolder: z
    .string()
    .optional()
    .describe("The parent folder path for create_folder."),
  folderName: z
    .string()
    .optional()
    .describe("The new folder name for create_folder."),
  newName: z.string().optional().describe("The new asset name for rename."),
  destinationPath: z
    .string()
    .optional()
    .describe("The destination asset path for move or duplicate."),
});

export function registerManageAssetTool(
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
  validateParams(params);

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params,
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to execute asset action '${params.action}'`
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

function validateParams(params: z.infer<typeof paramsSchema>) {
  switch (params.action) {
    case "create_folder":
      if (!params.parentFolder || !params.folderName) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "'create_folder' requires 'parentFolder' and 'folderName'"
        );
      }
      break;
    case "rename":
      if (!params.assetPath || !params.newName) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "'rename' requires 'assetPath' and 'newName'"
        );
      }
      break;
    case "move":
      if (!params.assetPath || !params.destinationPath) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "'move' requires 'assetPath' and 'destinationPath'"
        );
      }
      break;
    case "duplicate":
      if (!params.assetPath) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "'duplicate' requires 'assetPath'"
        );
      }
      break;
    case "delete":
      if (!params.assetPath) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "'delete' requires 'assetPath'"
        );
      }
      break;
  }
}