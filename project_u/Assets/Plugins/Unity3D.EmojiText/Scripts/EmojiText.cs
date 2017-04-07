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
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;
using System;

namespace ui
{
	public class EmojiText : Text, IPointerClickHandler
	{
		public EmojiConfig config;
		public Color hrefColor = Color.blue;

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

		struct PosHerfTuple
		{
			public int start;
			public int end;
			public string href;

			public PosHerfTuple(int s, int e, string href)
			{
				start = s;
				end = e;
				this.href = href;
			}
		}
		List<PosHerfTuple> hrefReplacements = new List<PosHerfTuple>();

		public delegate void OnHref(string href);
		public event OnHref onHref;

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
				return UpdateReplacements(base.text);
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

		protected override void OnDisable()
		{
			base.OnDisable();
			if (emojiCanvasRenderer != null)
				emojiCanvasRenderer.Clear();
        }

		string UpdateReplacements(string inputString)
		{
			hrefReplacements.Clear();
			var hrefReplaced = UpdateHrefReplacements(inputString);
			return UpdateEmojiReplacements(hrefReplaced);
		}

		readonly static Regex hrefMatcher = new Regex(@"\[([^\]]+)\]\(([^\]]+)\)");

		string UpdateHrefReplacements(string inputString)
		{
			var match = hrefMatcher.Match(inputString);
			if (match != null && match.Success)
			{
				var processed = inputString.Replace(match.Groups[0].ToString(), match.Groups[1].ToString());
				var start = match.Groups[0].Index;
				var end = start + match.Groups[1].Length;
				hrefReplacements.Add(new PosHerfTuple(start, end, match.Groups[2].ToString()));
				return UpdateHrefReplacements(processed);
			}
			return inputString;
		}

		void UpdateHrefPosTuples(int index, int count)
		{
			if (count > 1)
			{
				for (int i = 0; i < hrefReplacements.Count; ++i)
				{
					var h = hrefReplacements[i];
					if (index >= h.start)
					{
						// replace h and the rest
						h.end -= count;
						hrefReplacements[i] = h;
						for (int j = i + 1; j < hrefReplacements.Count; ++j)
						{
							var r = hrefReplacements[i];
							r.start -= count;
							r.end -= count;
							hrefReplacements[j] = r;
						}
					}
				}
			}
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
						UpdateHrefPosTuples(sb.Length - 1, 4);
					}
					else if (config.map.TryGetValue(doubleChar, out emojiIndex))
					{
						// Then check 32 bit emojis
						sb.Append(placeHolder);
						emojiReplacements.Add(new PosStringTuple(sb.Length - 1, emojiIndex));
						i += 2;
						UpdateHrefPosTuples(sb.Length - 1, 2);
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

		List<Vector3> hrefVh = new List<Vector3>();

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

			hrefVh.Clear();
			for (int i = 0; i < hrefReplacements.Count; ++i)
			{
				var h = hrefReplacements[i];
				for (int j = h.start; j < h.end; ++j)
				{
					var baseIndex = j * 4;
					if (baseIndex <= toFill.currentVertCount - 4)
					{
						for (int k = 0; k < 4; ++k)
						{
							toFill.PopulateUIVertex(ref tempVert, baseIndex + k);
							tempVerts[k] = tempVert;
							tempVert.color = hrefColor;
							toFill.SetUIVertex(tempVert, baseIndex + k);
						}
						hrefVh.Add(tempVerts[0].position);
						hrefVh.Add(tempVerts[1].position);
						hrefVh.Add(tempVerts[2].position);
					}
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
			if (IsActive())
			{
				if (emojiCanvasRenderer != null)
				{
					emojiCanvasRenderer.materialCount = 1;
					emojiCanvasRenderer.SetMaterial(materialForRendering, 0);
					emojiCanvasRenderer.SetTexture(config.texture);
				}
			}
		}

		protected virtual void RaycastOnHrefs(Vector2 sp, Camera eventCamera)
		{
			if (hrefReplacements.Count > 0)
			{
				Vector2 local;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out local);

				var hrefStartIndex = 0;
				for (int i = 0; i < hrefReplacements.Count; ++i)
				{
					var h = hrefReplacements[i];
					var count = h.end - h.start;
					for (int j = 0; j < count; ++j)
					{
						var baseIndex = (hrefStartIndex + j) * 3;
						var v0 = hrefVh[baseIndex];
						var v1 = hrefVh[baseIndex + 1];
						var v2 = hrefVh[baseIndex + 2];
						if (local.x < v0.x || local.x > v1.x)
						{
							continue;
						}
						if (local.y > v0.y || local.y < v2.y)
						{
							continue;
						}
						if (onHref != null)
						{
							onHref(h.href);
						}
					}
					hrefStartIndex += count;
				}

			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			RaycastOnHrefs(eventData.position, null);
		}
	}
}
