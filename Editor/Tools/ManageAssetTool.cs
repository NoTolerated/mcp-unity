using System;
using System.IO;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for managing Unity assets through the AssetDatabase.
    /// </summary>
    public class ManageAssetTool : McpToolBase
    {
        public ManageAssetTool()
        {
            Name = "manage_asset";
            Description = "Creates folders and renames, moves, duplicates, or deletes Unity assets through the AssetDatabase.";
        }

        public override JObject Execute(JObject parameters)
        {
            string action = parameters?["action"]?.ToObject<string>()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'action' not provided.",
                    "validation_error"
                );
            }

            try
            {
                switch (action)
                {
                    case "create_folder":
                        return CreateFolder(parameters);
                    case "rename":
                        return RenameAsset(parameters);
                    case "move":
                        return MoveAsset(parameters);
                    case "duplicate":
                        return DuplicateAsset(parameters);
                    case "delete":
                        return DeleteAsset(parameters);
                    default:
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Unsupported asset action '{action}'.",
                            "validation_error"
                        );
                }
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset action '{action}' failed: {ex.Message}",
                    "asset_error"
                );
            }
        }

        private static JObject CreateFolder(JObject parameters)
        {
            string parentFolder = NormalizeAssetPath(parameters["parentFolder"]?.ToObject<string>());
            string folderName = parameters["folderName"]?.ToObject<string>();

            if (string.IsNullOrEmpty(parentFolder) || string.IsNullOrEmpty(folderName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'create_folder' requires 'parentFolder' and 'folderName'.",
                    "validation_error"
                );
            }

            if (!IsAssetPath(parentFolder) || !AssetDatabase.IsValidFolder(parentFolder))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Parent folder '{parentFolder}' does not exist.",
                    "not_found_error"
                );
            }

            if (folderName.IndexOf('/') >= 0 || folderName.IndexOf('\\') >= 0)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'folderName' must be a single folder segment.",
                    "validation_error"
                );
            }

            string guid = AssetDatabase.CreateFolder(parentFolder, folderName);
            if (string.IsNullOrEmpty(guid))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Unity did not return a GUID for the new folder.",
                    "asset_error"
                );
            }

            string createdPath = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return CreateSuccessResponse("create_folder", $"Created folder '{createdPath}'.", createdPath, guid);
        }

        private static JObject RenameAsset(JObject parameters)
        {
            string assetPath = NormalizeAssetPath(parameters["assetPath"]?.ToObject<string>());
            string newName = parameters["newName"]?.ToObject<string>();

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(newName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'rename' requires 'assetPath' and 'newName'.",
                    "validation_error"
                );
            }

            if (!AssetExists(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset '{assetPath}' was not found.",
                    "not_found_error"
                );
            }

            string sanitizedName = Path.GetFileNameWithoutExtension(newName);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'newName' must contain a valid file or folder name.",
                    "validation_error"
                );
            }

            string error = AssetDatabase.RenameAsset(assetPath, sanitizedName);
            if (!string.IsNullOrEmpty(error))
            {
                return McpUnitySocketHandler.CreateErrorResponse(error, "asset_error");
            }

            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string extension = Path.GetExtension(assetPath);
            string renamedPath = string.IsNullOrEmpty(directory)
                ? $"Assets/{sanitizedName}{extension}"
                : $"{directory}/{sanitizedName}{extension}";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return CreateSuccessResponse("rename", $"Renamed asset to '{renamedPath}'.", renamedPath, AssetDatabase.AssetPathToGUID(renamedPath));
        }

        private static JObject MoveAsset(JObject parameters)
        {
            string assetPath = NormalizeAssetPath(parameters["assetPath"]?.ToObject<string>());
            string destinationPath = NormalizeAssetPath(parameters["destinationPath"]?.ToObject<string>());

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(destinationPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'move' requires 'assetPath' and 'destinationPath'.",
                    "validation_error"
                );
            }

            if (!AssetExists(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset '{assetPath}' was not found.",
                    "not_found_error"
                );
            }

            if (!IsAssetPath(destinationPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Destination path '{destinationPath}' must be inside the Unity Assets folder.",
                    "validation_error"
                );
            }

            string error = AssetDatabase.MoveAsset(assetPath, destinationPath);
            if (!string.IsNullOrEmpty(error))
            {
                return McpUnitySocketHandler.CreateErrorResponse(error, "asset_error");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return CreateSuccessResponse("move", $"Moved asset to '{destinationPath}'.", destinationPath, AssetDatabase.AssetPathToGUID(destinationPath));
        }

        private static JObject DuplicateAsset(JObject parameters)
        {
            string assetPath = NormalizeAssetPath(parameters["assetPath"]?.ToObject<string>());
            string destinationPath = NormalizeAssetPath(parameters["destinationPath"]?.ToObject<string>());

            if (string.IsNullOrEmpty(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'duplicate' requires 'assetPath'.",
                    "validation_error"
                );
            }

            if (!AssetExists(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset '{assetPath}' was not found.",
                    "not_found_error"
                );
            }

            if (string.IsNullOrEmpty(destinationPath))
            {
                destinationPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            }
            else if (!IsAssetPath(destinationPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Destination path '{destinationPath}' must be inside the Unity Assets folder.",
                    "validation_error"
                );
            }

            bool copied = AssetDatabase.CopyAsset(assetPath, destinationPath);
            if (!copied)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Unity failed to duplicate '{assetPath}' to '{destinationPath}'.",
                    "asset_error"
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return CreateSuccessResponse("duplicate", $"Duplicated asset to '{destinationPath}'.", destinationPath, AssetDatabase.AssetPathToGUID(destinationPath));
        }

        private static JObject DeleteAsset(JObject parameters)
        {
            string assetPath = NormalizeAssetPath(parameters["assetPath"]?.ToObject<string>());
            if (string.IsNullOrEmpty(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "'delete' requires 'assetPath'.",
                    "validation_error"
                );
            }

            if (!AssetExists(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset '{assetPath}' was not found.",
                    "not_found_error"
                );
            }

            bool deleted = AssetDatabase.DeleteAsset(assetPath);
            if (!deleted)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Unity failed to delete '{assetPath}'.",
                    "asset_error"
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["action"] = "delete",
                ["message"] = $"Deleted asset '{assetPath}'.",
                ["path"] = assetPath
            };
        }

        private static JObject CreateSuccessResponse(string action, string message, string assetPath, string guid)
        {
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["action"] = action,
                ["message"] = message,
                ["path"] = assetPath,
                ["guid"] = guid ?? string.Empty
            };
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalized = path.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    .Replace('\\', '/')
                    .TrimEnd('/');

                string fullPath = Path.GetFullPath(normalized).Replace('\\', '/');
                if (fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = fullPath.Substring(projectRoot.Length + 1);
                }
            }

            return normalized;
        }

        private static bool IsAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase));
        }

        private static bool AssetExists(string assetPath)
        {
            return IsAssetPath(assetPath) &&
                   (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null || AssetDatabase.IsValidFolder(assetPath));
        }
    }
}