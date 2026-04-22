using System;
using System.IO;
using System.Threading.Tasks;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for capturing Unity Scene view screenshots to PNG files.
    /// </summary>
    public class TakeScreenshotTool : McpToolBase
    {
        public TakeScreenshotTool()
        {
            Name = "take_screenshot";
            Description = "Captures the Unity Scene view to a PNG file and returns the saved file path with image metadata.";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    tcs.TrySetResult(CaptureScreenshot(parameters));
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to capture screenshot: {ex.Message}",
                        "capture_error"
                    ));
                }
            };
        }

        private static JObject CaptureScreenshot(JObject parameters)
        {
            string mode = parameters?["mode"]?.ToObject<string>() ?? "scene";
            if (!string.Equals(mode, "scene", StringComparison.OrdinalIgnoreCase))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Only 'scene' mode is supported in this version.",
                    "not_supported_error"
                );
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null && SceneView.sceneViews.Count > 0)
            {
                sceneView = SceneView.sceneViews[0] as SceneView;
            }

            if (sceneView == null || sceneView.camera == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "No active Scene view camera is available for screenshot capture.",
                    "not_found_error"
                );
            }

            int width = Mathf.Clamp(
                parameters?["width"]?.ToObject<int?>() ?? Mathf.Max(32, Mathf.RoundToInt(sceneView.position.width)),
                32,
                4096);
            int height = Mathf.Clamp(
                parameters?["height"]?.ToObject<int?>() ?? Mathf.Max(32, Mathf.RoundToInt(sceneView.position.height)),
                32,
                4096);

            string outputPath = ResolveOutputPath(parameters?["filePath"]?.ToObject<string>());
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            RenderTexture renderTexture = null;
            Texture2D texture = null;
            RenderTexture previousActive = RenderTexture.active;
            Camera camera = sceneView.camera;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);

                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                byte[] pngBytes = ImageConversion.EncodeToPNG(texture);
                File.WriteAllBytes(outputPath, pngBytes);

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Captured Scene view screenshot to '{outputPath}'.",
                    ["mode"] = "scene",
                    ["path"] = outputPath,
                    ["projectRelativePath"] = GetProjectRelativePath(outputPath),
                    ["width"] = width,
                    ["height"] = height,
                    ["sizeBytes"] = pngBytes.LongLength
                };
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        private static string ResolveOutputPath(string filePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string defaultDirectory = Path.Combine(projectRoot, "Temp", "McpUnityScreenshots");

            string resolvedPath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(defaultDirectory, $"scene_{DateTime.Now:yyyyMMdd_HHmmssfff}.png")
                : filePath;

            if (!Path.IsPathRooted(resolvedPath))
            {
                resolvedPath = Path.Combine(projectRoot, resolvedPath);
            }

            if (string.IsNullOrEmpty(Path.GetExtension(resolvedPath)))
            {
                resolvedPath += ".png";
            }

            return Path.GetFullPath(resolvedPath);
        }

        private static string GetProjectRelativePath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalizedProjectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedPath = absolutePath.Replace('\\', '/');

            if (normalizedPath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Substring(normalizedProjectRoot.Length + 1);
            }

            return string.Empty;
        }
    }
}