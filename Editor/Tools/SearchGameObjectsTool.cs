using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for searching loaded scenes for GameObjects that match a set of filters.
    /// </summary>
    public class SearchGameObjectsTool : McpToolBase
    {
        public SearchGameObjectsTool()
        {
            Name = "search_gameobjects";
            Description = "Searches loaded Unity scenes for GameObjects by name, tag, layer, active state, component type, or parent hierarchy.";
        }

        public override JObject Execute(JObject parameters)
        {
            string nameFilter = parameters?["name"]?.ToObject<string>();
            bool useRegex = parameters?["useRegex"]?.ToObject<bool?>() ?? false;
            bool exactMatch = parameters?["exactMatch"]?.ToObject<bool?>() ?? false;
            string tagFilter = parameters?["tag"]?.ToObject<string>();
            int? layerFilter = parameters?["layer"]?.ToObject<int?>();
            string layerNameFilter = parameters?["layerName"]?.ToObject<string>();
            bool? isActiveFilter = parameters?["isActive"]?.ToObject<bool?>();
            string componentTypeFilter = parameters?["componentType"]?.ToObject<string>();
            string parentPath = parameters?["parentPath"]?.ToObject<string>();
            int? parentId = parameters?["parentId"]?.ToObject<int?>();
            bool includeInactive = parameters?["includeInactive"]?.ToObject<bool?>() ?? true;
            int limit = Mathf.Clamp(parameters?["limit"]?.ToObject<int?>() ?? 100, 1, 500);

            if (parentId.HasValue && !string.IsNullOrEmpty(parentPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Use either 'parentId' or 'parentPath', not both.",
                    "validation_error"
                );
            }

            if (!string.IsNullOrEmpty(layerNameFilter))
            {
                int resolvedLayer = LayerMask.NameToLayer(layerNameFilter);
                if (resolvedLayer < 0)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Layer '{layerNameFilter}' does not exist.",
                        "validation_error"
                    );
                }

                layerFilter = resolvedLayer;
            }

            Regex nameRegex = null;
            if (!string.IsNullOrEmpty(nameFilter) && useRegex)
            {
                try
                {
                    nameRegex = new Regex(nameFilter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException ex)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Invalid name regex: {ex.Message}",
                        "validation_error"
                    );
                }
            }

            GameObject parentObject = null;
            if (parentId.HasValue || !string.IsNullOrEmpty(parentPath))
            {
                JObject parentError = FindParentGameObject(parentId, parentPath, out parentObject);
                if (parentError != null)
                {
                    return parentError;
                }
            }

            List<JObject> matches = new List<JObject>();

            if (parentObject != null)
            {
                TraverseHierarchy(parentObject, includeInactive, gameObject =>
                {
                    if (matches.Count >= limit)
                    {
                        return false;
                    }

                    if (MatchesFilters(gameObject, nameFilter, nameRegex, exactMatch, tagFilter, layerFilter, isActiveFilter, componentTypeFilter))
                    {
                        matches.Add(ToResult(gameObject));
                    }

                    return matches.Count < limit;
                });
            }
            else
            {
                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount && matches.Count < limit; sceneIndex++)
                {
                    Scene scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    foreach (GameObject rootObject in scene.GetRootGameObjects())
                    {
                        if (matches.Count >= limit)
                        {
                            break;
                        }

                        TraverseHierarchy(rootObject, includeInactive, gameObject =>
                        {
                            if (matches.Count >= limit)
                            {
                                return false;
                            }

                            if (MatchesFilters(gameObject, nameFilter, nameRegex, exactMatch, tagFilter, layerFilter, isActiveFilter, componentTypeFilter))
                            {
                                matches.Add(ToResult(gameObject));
                            }

                            return matches.Count < limit;
                        });
                    }
                }
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Found {matches.Count} GameObject(s) matching the requested filters.",
                ["count"] = matches.Count,
                ["results"] = new JArray(matches)
            };
        }

        private static JObject FindParentGameObject(int? parentId, string parentPath, out GameObject parentObject)
        {
            parentObject = null;

            if (parentId.HasValue)
            {
                parentObject = EditorUtility.InstanceIDToObject(parentId.Value) as GameObject;
            }
            else if (!string.IsNullOrEmpty(parentPath))
            {
                parentObject = GameObject.Find(parentPath) ?? FindGameObjectByPathAcrossScenes(parentPath);
            }

            if (parentObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "The requested parent GameObject could not be found.",
                    "not_found_error"
                );
            }

            return null;
        }

        private static GameObject FindGameObjectByPathAcrossScenes(string objectPath)
        {
            if (string.IsNullOrEmpty(objectPath))
            {
                return null;
            }

            string normalizedPath = objectPath.TrimStart('/');
            string[] pathParts = normalizedPath.Split('/');
            if (pathParts.Length == 0)
            {
                return null;
            }

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    if (!string.Equals(rootObject.name, pathParts[0], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    GameObject current = rootObject;
                    bool matched = true;
                    for (int index = 1; index < pathParts.Length; index++)
                    {
                        Transform child = current.transform.Find(pathParts[index]);
                        if (child == null)
                        {
                            matched = false;
                            break;
                        }

                        current = child.gameObject;
                    }

                    if (matched)
                    {
                        return current;
                    }
                }
            }

            return null;
        }

        private static void TraverseHierarchy(GameObject rootObject, bool includeInactive, Func<GameObject, bool> visitor)
        {
            if (rootObject == null)
            {
                return;
            }

            if ((includeInactive || rootObject.activeInHierarchy) && !visitor(rootObject))
            {
                return;
            }

            foreach (Transform child in rootObject.transform)
            {
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                TraverseHierarchy(child.gameObject, includeInactive, visitor);
            }
        }

        private static bool MatchesFilters(
            GameObject gameObject,
            string nameFilter,
            Regex nameRegex,
            bool exactMatch,
            string tagFilter,
            int? layerFilter,
            bool? isActiveFilter,
            string componentTypeFilter)
        {
            if (!string.IsNullOrEmpty(nameFilter))
            {
                bool nameMatched = nameRegex != null
                    ? nameRegex.IsMatch(gameObject.name)
                    : exactMatch
                        ? string.Equals(gameObject.name, nameFilter, StringComparison.OrdinalIgnoreCase)
                        : gameObject.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!nameMatched)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(tagFilter) && !string.Equals(gameObject.tag, tagFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (layerFilter.HasValue && gameObject.layer != layerFilter.Value)
            {
                return false;
            }

            if (isActiveFilter.HasValue && gameObject.activeInHierarchy != isActiveFilter.Value)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(componentTypeFilter) && !HasComponentType(gameObject, componentTypeFilter))
            {
                return false;
            }

            return true;
        }

        private static bool HasComponentType(GameObject gameObject, string componentTypeFilter)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();
                if (string.Equals(componentType.Name, componentTypeFilter, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(componentType.FullName, componentTypeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static JObject ToResult(GameObject gameObject)
        {
            JArray components = new JArray();
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                components.Add(component == null ? "MissingScript" : component.GetType().Name);
            }

            return new JObject
            {
                ["name"] = gameObject.name,
                ["path"] = GameObjectToolUtils.GetGameObjectPath(gameObject),
                ["instanceId"] = gameObject.GetInstanceID(),
                ["scene"] = gameObject.scene.name,
                ["activeSelf"] = gameObject.activeSelf,
                ["activeInHierarchy"] = gameObject.activeInHierarchy,
                ["tag"] = gameObject.tag,
                ["layer"] = gameObject.layer,
                ["layerName"] = LayerMask.LayerToName(gameObject.layer),
                ["childCount"] = gameObject.transform.childCount,
                ["components"] = components
            };
        }
    }
}