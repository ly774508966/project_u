﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace lua
{
	class WatchingLuaSources : AssetPostprocessor 
	{
		enum Status 
		{
			Ok,
			Reimported,
		};
		static Dictionary<string, Status> luaSourceStatus = new Dictionary<string, Status>();

		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) 
		{
			foreach (string str in importedAssets)
			{
				if (str.EndsWith(".lua"))
				{
					luaSourceStatus[str] = Status.Reimported;
				}
			}
			foreach (string str in deletedAssets) 
			{
				if (str.EndsWith(".lua"))
				{
					luaSourceStatus.Remove(str);
				}
			}	

			for (int i=0; i<movedAssets.Length; i++)
			{
				var movedAsset = movedAssets[i];
				var movedFromAssetPath = movedFromAssetPaths[i];
				if (movedAsset.EndsWith(".lua"))
				{
					luaSourceStatus.Remove(movedFromAssetPath);
					luaSourceStatus[movedAsset] = Status.Reimported;
				}
			}
		}

		public static bool IsReimported(string path)
		{
			var rootPath = new System.Uri(Application.dataPath);
			var scriptPath = new System.Uri(path);
			var relativeUri = rootPath.MakeRelativeUri(scriptPath);
			Status s;
			if (luaSourceStatus.TryGetValue(relativeUri.ToString(), out s))
			{
				return s == Status.Reimported;
			}
			return false;
		}

		public static void SetProcessed(string path)
		{
			if (luaSourceStatus.ContainsKey(path))
			{
				luaSourceStatus[path] = Status.Ok;
			}
		}

	}
}
