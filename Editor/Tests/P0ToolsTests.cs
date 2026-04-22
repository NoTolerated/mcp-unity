using System.Collections;
using System.IO;
using System.Threading.Tasks;
using McpUnity.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace McpUnity.Tests
{
    /// <summary>
    /// Regression tests for the first P0 MCP Unity tools.
    /// </summary>
    public class P0ToolsTests
    {
        private const string TestRootFolder = "Assets/McpUnityP0Tests";
        private GameObject _searchRoot;
        private ManageAssetTool _manageAssetTool;

        [SetUp]
        public void SetUp()
        {
            _manageAssetTool = new ManageAssetTool();

            if (AssetDatabase.IsValidFolder(TestRootFolder))
            {
                AssetDatabase.DeleteAsset(TestRootFolder);
            }

            AssetDatabase.CreateFolder("Assets", "McpUnityP0Tests");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (_searchRoot != null)
            {
                Object.DestroyImmediate(_searchRoot);
                _searchRoot = null;
            }

            if (AssetDatabase.IsValidFolder(TestRootFolder))
            {
                AssetDatabase.DeleteAsset(TestRootFolder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void SearchGameObjectsTool_FindsChildByComponentAndParentPath()
        {
            _searchRoot = new GameObject("P0ToolsRoot");
            GameObject child = new GameObject("RigidChild");
            child.AddComponent<Rigidbody>();
            child.transform.SetParent(_searchRoot.transform, false);

            SearchGameObjectsTool tool = new SearchGameObjectsTool();
            JObject result = tool.Execute(new JObject
            {
                ["componentType"] = nameof(Rigidbody),
                ["parentPath"] = "/P0ToolsRoot",
                ["includeInactive"] = true,
                ["limit"] = 10
            });

            Assert.AreEqual(true, result["success"]?.ToObject<bool>());
            Assert.AreEqual(1, result["count"]?.ToObject<int>());

            JArray results = (JArray)result["results"];
            Assert.IsNotNull(results);
            Assert.AreEqual("/P0ToolsRoot/RigidChild", results[0]?["path"]?.ToString());
        }

        [Test]
        public void ManageAssetTool_SupportsCreateRenameMoveDuplicateAndDelete()
        {
            JObject createFolderResult = _manageAssetTool.Execute(new JObject
            {
                ["action"] = "create_folder",
                ["parentFolder"] = TestRootFolder,
                ["folderName"] = "Nested"
            });

            Assert.AreEqual(true, createFolderResult["success"]?.ToObject<bool>());
            Assert.IsTrue(AssetDatabase.IsValidFolder(TestRootFolder + "/Nested"));

            string assetPath = CreateTextAsset("source.txt", "hello");

            JObject renameResult = _manageAssetTool.Execute(new JObject
            {
                ["action"] = "rename",
                ["assetPath"] = assetPath,
                ["newName"] = "renamed"
            });

            string renamedPath = renameResult["path"]?.ToString();
            Assert.AreEqual(true, renameResult["success"]?.ToObject<bool>());
            Assert.AreEqual(TestRootFolder + "/renamed.txt", renamedPath);

            JObject moveResult = _manageAssetTool.Execute(new JObject
            {
                ["action"] = "move",
                ["assetPath"] = renamedPath,
                ["destinationPath"] = TestRootFolder + "/Nested/moved.txt"
            });

            string movedPath = moveResult["path"]?.ToString();
            Assert.AreEqual(true, moveResult["success"]?.ToObject<bool>());
            Assert.AreEqual(TestRootFolder + "/Nested/moved.txt", movedPath);

            JObject duplicateResult = _manageAssetTool.Execute(new JObject
            {
                ["action"] = "duplicate",
                ["assetPath"] = movedPath,
                ["destinationPath"] = TestRootFolder + "/Nested/moved_copy.txt"
            });

            string duplicatePath = duplicateResult["path"]?.ToString();
            Assert.AreEqual(true, duplicateResult["success"]?.ToObject<bool>());
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(duplicatePath));

            JObject deleteResult = _manageAssetTool.Execute(new JObject
            {
                ["action"] = "delete",
                ["assetPath"] = duplicatePath
            });

            Assert.AreEqual(true, deleteResult["success"]?.ToObject<bool>());
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<TextAsset>(duplicatePath));
        }

        [UnityTest]
        public IEnumerator TakeScreenshotTool_WithUnsupportedMode_ReturnsError()
        {
            TakeScreenshotTool tool = new TakeScreenshotTool();
            var tcs = new TaskCompletionSource<JObject>();

            tool.ExecuteAsync(new JObject
            {
                ["mode"] = "game"
            }, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;
            Assert.AreEqual("not_supported_error", result["error"]?["type"]?.ToString());
        }

        private static string CreateTextAsset(string fileName, string content)
        {
            string absolutePath = Path.Combine(Application.dataPath, "McpUnityP0Tests", fileName);
            File.WriteAllText(absolutePath, content);
            string assetPath = TestRootFolder + "/" + fileName;
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
            return assetPath;
        }
    }
}