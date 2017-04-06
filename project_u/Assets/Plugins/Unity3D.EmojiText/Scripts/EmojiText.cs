// idea and part of code ref, https://github.com/mcraiha/Unity-UI-emoji
/*
MIT License

Copyright (c) 2016 xiaobin83

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ui
{
	public class EmojiText : Text
	{
		public EmojiConfig config;

		private static char placeHolder = 'M';

		struct PosStringTuple
		{
			public int pos;
			public int emoji;

			public PosStringTuple(int p, int s)
			{
				this.pos = p;
				this.emoji = s;
			}
		}
		List<PosStringTuple> emojiReplacements = new List<PosStringTuple>();

		public string rawText
		{
			get
			{
				return base.text;
			}
		}

		public override string text
		{
			get
			{
				return UpdateEmojiReplacements(base.text);
			}
			set
			{
				base.text = text;
			}
		}

		CanvasRenderer emojiCanvasRenderer;

		protected override void Awake()
		{
			var go = new GameObject("emoji");
			go.hideFlags = HideFlags.HideAndDontSave;
			go.transform.SetParent(transform, false);
			emojiCanvasRenderer = go.AddComponent<CanvasRenderer>();
			emojiCanvasRenderer.hideFlags = HideFlags.HideAndDontSave;
			base.Awake();
		}

		protected override void OnDestroy()
		{
			if (emojiCanvasRenderer != null)
			{
				if (Application.isPlaying)
					Destroy(emojiCanvasRenderer.gameObject);
				else
					DestroyImmediate(emojiCanvasRenderer.gameObject);
				emojiCanvasRenderer = null;
			}
			base.OnDestroy();
		}

		string UpdateEmojiReplacements(string inputString)
		{
			if (!string.IsNullOrEmpty(inputString))
			{
				var sb = new System.Text.StringBuilder();

				emojiReplacements.Clear();

				int i = 0;
				while (i < inputString.Length)
				{
					string singleChar = inputString.Substring(i, 1);
					string doubleChar = "";
					string fourChar = "";

					if (i < (inputString.Length - 1))
					{
						doubleChar = inputString.Substring(i, 2);
					}

					if (i < (inputString.Length - 3))
					{
						fourChar = inputString.Substring(i, 4);
					}

					int emojiIndex;
					if (config.map.TryGetValue(fourChar, out emojiIndex))
					{
						// Check 64 bit emojis first
						sb.Append(placeHolder);
						emojiReplacements.Add(new PosStringTuple(sb.Length - 1, emojiIndex));
						i += 4;
					}
					else if (config.map.TryGetValue(doubleChar, out emojiIndex))
					{
						// Then check 32 bit emojis
						sb.Append(placeHolder);
						emojiReplacements.Add(new PosStringTuple(sb.Length - 1, emojiIndex));
						i += 2;
					}
					else if (config.map.TryGetValue(singleChar, out emojiIndex))
					{
						// Finally check 16 bit emojis
						sb.Append(placeHolder);
						emojiReplacements.Add(new PosStringTuple(sb.Length - 1, emojiIndex));
						i++;
					}
					else
					{
						sb.Append(inputString[i]);
						i++;
					}
				}
				return sb.ToString();
			}
			return string.Empty;
		}

		readonly UIVertex[] tempVerts = new UIVertex[4];

		readonly static VertexHelper emojiVh = new VertexHelper();

		static Mesh emojiWorkMesh_;
		static Mesh emojiWorkMesh
		{
			get
			{
				if (emojiWorkMesh_ == null)
				{
					emojiWorkMesh_ = new Mesh();
					emojiWorkMesh_.name = "Shared Emoji Mesh";
					emojiWorkMesh_.hideFlags = HideFlags.HideAndDontSave;
				}
				return emojiWorkMesh_;
			}
		}


		protected override void OnPopulateMesh(VertexHelper toFill)
		{
			base.OnPopulateMesh(toFill);
			emojiVh.Clear();
			UIVertex tempVert = new UIVertex();
			for (int i = 0; i < emojiReplacements.Count; ++i)
			{
				var r = emojiReplacements[i];
				var emojiPosInString = r.pos;
				var emojiRect = config.rects[r.emoji];

				int baseIndex = emojiPosInString * 4;
				if (baseIndex <= toFill.currentVertCount - 4)
				{
					for (int j = 0; j < 4; ++j)
					{
						toFill.PopulateUIVertex(ref tempVert, baseIndex + j);
						tempVerts[j] = tempVert;
						tempVert.color = Color.clear;
						toFill.SetUIVertex(tempVert, baseIndex + j);
					}
					tempVerts[0].uv0 = new Vector2(emojiRect.x, emojiRect.yMax);
					tempVerts[1].uv0 = new Vector2(emojiRect.xMax, emojiRect.yMax);
					tempVerts[2].uv0 = new Vector2(emojiRect.xMax, emojiRect.y);
					tempVerts[3].uv0 = new Vector2(emojiRect.x, emojiRect.y);
					emojiVh.AddUIVertexQuad(tempVerts);
				}
			}
		}

		protected override void UpdateGeometry()
		{
			base.UpdateGeometry();
			if (emojiCanvasRenderer != null)
			{
				emojiVh.FillMesh(emojiWorkMesh);
				emojiCanvasRenderer.SetMesh(emojiWorkMesh);
			}
		}

		protected override void UpdateMaterial()
		{
			base.UpdateMaterial();
			if (!IsActive())
				return;
			if (emojiCanvasRenderer != null)
			{
				emojiCanvasRenderer.materialCount = 1;
				emojiCanvasRenderer.SetMaterial(materialForRendering, 0);
				emojiCanvasRenderer.SetTexture(config.texture);
			}
		}



	}
}
