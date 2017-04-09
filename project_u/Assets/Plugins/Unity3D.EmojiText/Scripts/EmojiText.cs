﻿// idea and part of code ref, https://github.com/mcraiha/Unity-UI-emoji
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
using UnityEngine.Events;
using UnityEngine.Serialization;
using System;


namespace ui
{
	public class EmojiText : Text, 
		IPointerClickHandler,
		IPointerDownHandler, IPointerUpHandler,
		IPointerEnterHandler, IPointerExitHandler
	{
		[FormerlySerializedAs("config")]
		[SerializeField]
		EmojiConfig m_Config;
		EmojiConfig config
		{
			get
			{
				return m_Config;
			}
			set
			{
				m_Config = value;
				if (shouldEmojilize)
				{
					CreateEmojiCanvasRenderer();
				}
			}
		}
		public Color hrefColor = Color.blue;
		[Serializable]
		public class HrefClickedEvent : UnityEvent<string, string> {}
		[SerializeField]
		private HrefClickedEvent hrefOnClickedEvent = new HrefClickedEvent();
		public string hrefOnClickedEventName = "OnHrefClicked";
		public bool escapeUnicodeCharacter = true;

		public bool altPredefinedStringColor = false;
		public Color predefinedStringColor = Color.green;

		public bool showRawText = false;

		public System.Func<string, string> willInsertBackOnePredefinedString;

		const string placeHolderFmt = "<size={0}>M</size>";
		string GetPlaceHolder()
		{
			if (config != null)
				return string.Format(placeHolderFmt, Mathf.FloorToInt(config.sizeFactor * fontSize));
			else
				return string.Format(placeHolderFmt, fontSize);
        }

		struct PosEmojiTuple
		{
			public int pos;
			public int emoji;
			public PosEmojiTuple(int p, int s)
			{
				this.pos = p;
				this.emoji = s;
			}
		}
		List<PosEmojiTuple> emojiReplacements = new List<PosEmojiTuple>();

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


		struct PosPredefinedTuple
		{
			public int pos;
			public string predefined;

			public PosPredefinedTuple(int p, string pre)
			{
				pos = p;
				predefined = pre;
			}
		}
		List<PosPredefinedTuple> predefinedReplacements = new List<PosPredefinedTuple>();

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
				if (base.text != value)
				{
					base.text = value;
				}
			}
		}

		public float characterBaseline = 0;
		public float emojiBaseline = 0;

		CanvasRenderer emojiCanvasRenderer;

		bool shouldEmojilize
		{
			get
			{
				return config != null && !showRawText;
            }
        }

		void CreateEmojiCanvasRenderer()
		{
			if (shouldEmojilize && emojiCanvasRenderer == null)
			{
				var trans = transform.FindChild("__emoji");
				if (trans != null)
				{
					emojiCanvasRenderer = trans.GetComponent<CanvasRenderer>();
					trans.gameObject.hideFlags = HideFlags.HideAndDontSave;
				}
				if (emojiCanvasRenderer == null)
				{
					var go = new GameObject("__emoji");
					emojiCanvasRenderer = go.AddComponent<CanvasRenderer>();
					emojiCanvasRenderer.hideFlags = HideFlags.HideAndDontSave;
					go.hideFlags = HideFlags.HideAndDontSave;
					go.transform.SetParent(transform, false);
				}
			}
		}

		protected override void Awake()
		{
			CreateEmojiCanvasRenderer();
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

		readonly static Regex predefinedMatcher = new Regex(@"`([^`]*)`");
		string UpdatePredefinedReplacements(string inputString)
		{
			var match = predefinedMatcher.Match(inputString);
			if (match != null && match.Success)
			{
				// remove predefined
				var processed = inputString.Substring(0, match.Index) + inputString.Substring(match.Index + match.Length);
				predefinedReplacements.Add(new PosPredefinedTuple(match.Index, match.Groups[1].ToString()));
				return UpdatePredefinedReplacements(processed);
			}
			return inputString;
		}

		string InsertBackPredefinedReplacements(string inputString)
		{
			for (int i = 0; i < predefinedReplacements.Count; ++i)
			{
				var p = predefinedReplacements[i];
				var toInsert = p.predefined;
				if (willInsertBackOnePredefinedString != null)
					toInsert = willInsertBackOnePredefinedString(toInsert);
				if (altPredefinedStringColor)
				{
					toInsert = string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGBA(predefinedStringColor), toInsert);
				}
				inputString = inputString.Substring(0, p.pos) + toInsert + inputString.Substring(p.pos);
				InsertPosHrefTuples(p.pos, toInsert.Length);
				InsertPosEmojiTuples(p.pos, toInsert.Length);
				InsertPosPredefinedTuples(i + 1, toInsert.Length);
			}
			return inputString;
		}

		readonly static Regex unicodeEscapeMatcher = new Regex(@"\\[uU]([0-9a-fA-F]+)");
		static string EscapeUnicodeChar(string inputString)
		{
			var match = unicodeEscapeMatcher.Match(inputString);
			if (match != null && match.Success)
			{
				var ch = char.ConvertFromUtf32(System.Convert.ToInt32(match.Groups[1].ToString(), 16));
				var processed = inputString.Replace(match.Groups[0].ToString(), ch);
				return EscapeUnicodeChar(processed);
			}
			return inputString;
		}

		string UpdateReplacements(string inputString)
		{
			if (!shouldEmojilize)
				return inputString;

			predefinedReplacements.Clear();
			inputString = UpdatePredefinedReplacements(inputString);

			if (escapeUnicodeCharacter)
				inputString = EscapeUnicodeChar(inputString);

			hrefReplacements.Clear();
			inputString = UpdateHrefReplacements(inputString);

			inputString = UpdateEmojiReplacements(inputString);
			inputString = InsertBackPredefinedReplacements(inputString);
			return inputString;
		}



		readonly static Regex hrefMatcher = new Regex(@"\[([^\]]+)\]\(([^\]]+)\)");
		string UpdateHrefReplacements(string inputString)
		{
			var match = hrefMatcher.Match(inputString);
			if (match != null && match.Success)
			{
				var processed = inputString.Substring(0, match.Index) + match.Groups[1].ToString() + inputString.Substring(match.Index + match.Length);
				var start = match.Groups[0].Index;
				var end = start + match.Groups[1].Length;
				hrefReplacements.Add(new PosHerfTuple(start, end, match.Groups[2].ToString()));
				UpdatePosPredefinedTuples(start, end - start - match.Length);
				return UpdateHrefReplacements(processed);
			}
			return inputString;
		}

		void InsertPosPredefinedTuples(int predefinedReplacementStartIndex, int count)
		{
			if (count != 0)
			{
				for (int i = predefinedReplacementStartIndex; i < predefinedReplacements.Count; ++i)
				{
					var p = predefinedReplacements[i];
					p.pos += count;
					predefinedReplacements[i] = p;
				}
			}
		}

		void UpdatePosPredefinedTuples(int index, int count)
		{
			if (count != 0)
			{
				for (int i = 0; i < predefinedReplacements.Count; ++i)
				{
					var p = predefinedReplacements[i];
					if (index < p.pos)
					{
						p.pos += count;
						predefinedReplacements[i] = p;
						for (int j = i + 1; j < predefinedReplacements.Count; ++j)
						{
							p = predefinedReplacements[j];
							p.pos += count;
							predefinedReplacements[j] = p;
						}
						return;
					}
				}
			}
		}

		void InsertPosEmojiTuples(int index, int count)
		{
			if (count != 0)
			{
				for (int i = 0; i < emojiReplacements.Count; ++i)
					{
						var e = emojiReplacements[i];
						if (index <= e.pos)
						{
							e.pos += count;
							emojiReplacements[i] = e;
							for (int j = i + 1; j < emojiReplacements.Count; ++j)
							{
								e = emojiReplacements[j];
								e.pos += count;
								emojiReplacements[j] = e;
							}
							return;
						}
					}
			}
		}

		void UpdatePosEmojiTuples(int index, int count)
		{
			if (count != 0)
			{
				for (int i = 0; i < emojiReplacements.Count; ++i)
					{
						var e = emojiReplacements[i];
						if (index < e.pos)
						{
							e.pos += count;
							emojiReplacements[i] = e;
							for (int j = i + 1; j < emojiReplacements.Count; ++j)
							{
								e = emojiReplacements[j];
								e.pos += count;
								emojiReplacements[j] = e;
							}
							return;
						}
					}
			}
		}

		void InsertPosHrefTuples(int index, int count)
		{
			if (count != 0)
			{
				for (int i = 0; i < hrefReplacements.Count; ++i)
				{
					var h = hrefReplacements[i];
					if (index <= h.start || index < h.end)
					{
						if (index <= h.start)
							h.start += count;
						h.end += count;
						hrefReplacements[i] = h;
						for (int j = i + 1; j < hrefReplacements.Count; ++j)
						{
							h = hrefReplacements[j];
							h.start += count;
							h.end += count;
							hrefReplacements[j] = h;
						}
						return;
					}
				}
			}
		}

		void UpdatePosHrefTuples(int index, int count)
		{
			if (count != 0)
			{
				for (int i = 0; i < hrefReplacements.Count; ++i)
				{
					var h = hrefReplacements[i];
					if (index < h.start || index < h.end)
					{
						if (index < h.start)
							h.start += count;
						h.end += count;
						hrefReplacements[i] = h;
						for (int j = i + 1; j < hrefReplacements.Count; ++j)
						{
							h = hrefReplacements[j];
							h.start += count;
							h.end += count;
							hrefReplacements[j] = h;
						}
						return;
					}
				}
			}
		}

		string UpdateEmojiReplacements(string inputString)
		{
			emojiReplacements.Clear();

			if (!string.IsNullOrEmpty(inputString))
			{
				var sb = new System.Text.StringBuilder();

				string placeHolder = GetPlaceHolder();
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
						var emojiStart = sb.Length;
						sb.Append(placeHolder);
						var emojiCharStart = sb.Length - 1 - 7; // 1 -> emoji char,  7 -> length of "</size>"
						emojiReplacements.Add(new PosEmojiTuple(emojiCharStart, emojiIndex));
						UpdatePosPredefinedTuples(emojiStart, placeHolder.Length - 4);
						UpdatePosHrefTuples(emojiStart, placeHolder.Length - 4);
						i += 4;
					}
					else if (config.map.TryGetValue(doubleChar, out emojiIndex))
					{
						// Then check 32 bit emojis
						var emojiStart = sb.Length;
						sb.Append(placeHolder);
						var emojiCharStart = sb.Length - 1 - 7; // 1 -> emoji char,  7 -> length of "</size>"
						emojiReplacements.Add(new PosEmojiTuple(emojiCharStart, emojiIndex));
						UpdatePosPredefinedTuples(emojiStart, placeHolder.Length - 2);
						UpdatePosHrefTuples(emojiStart, placeHolder.Length - 2);
						i += 2;
					}
					else if (config.map.TryGetValue(singleChar, out emojiIndex))
					{
						var emojiStart = sb.Length;
						sb.Append(placeHolder);
						var emojiCharStart = sb.Length - 1 - 7; // 1 -> emoji char,  7 -> length of "</size>"
						emojiReplacements.Add(new PosEmojiTuple(emojiCharStart, emojiIndex));
						UpdatePosPredefinedTuples(emojiStart, placeHolder.Length - 1);
						UpdatePosHrefTuples(emojiStart, placeHolder.Length - 1);
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

		List<float> hrefVh = new List<float>();

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
			if (shouldEmojilize)
			{
				UIVertex tempVert = new UIVertex();

				if (characterBaseline != 0f)
				{
					for (int i = 0; i < toFill.currentVertCount; ++i)
					{
						toFill.PopulateUIVertex(ref tempVert, i);
						tempVert.position = new Vector3(tempVert.position.x, tempVert.position.y + characterBaseline, tempVert.position.z);
						toFill.SetUIVertex(tempVert, i);
					}
				}

				emojiVh.Clear();
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
							tempVert.position = new Vector3(tempVert.position.x, tempVert.position.y + emojiBaseline, tempVert.position.z);
							tempVerts[j] = tempVert;
							tempVert.color = Color.clear;
							toFill.SetUIVertex(tempVert, baseIndex + j);
						}
						tempVerts[0].color = Color.white;
						tempVerts[0].uv0 = new Vector2(emojiRect.x, emojiRect.yMax);
						tempVerts[1].color = Color.white;
						tempVerts[1].uv0 = new Vector2(emojiRect.xMax, emojiRect.yMax);
						tempVerts[2].color = Color.white;
						tempVerts[2].uv0 = new Vector2(emojiRect.xMax, emojiRect.y);
						tempVerts[3].color = Color.white;
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
							hrefVh.Add(tempVerts[0].position.x);
							hrefVh.Add(tempVerts[1].position.x);
							hrefVh.Add(tempVerts[2].position.y);
							hrefVh.Add(tempVerts[0].position.y);
						}
					}
				}
			}
		}

		protected override void UpdateGeometry()
		{
			base.UpdateGeometry();
			if (shouldEmojilize)
			{
				CreateEmojiCanvasRenderer();

				emojiVh.FillMesh(emojiWorkMesh);
				emojiCanvasRenderer.SetMesh(emojiWorkMesh);
			}
		}
		readonly static List<Component> components = new List<Component>();
		Material GetModifiedEmojiMaterial(Material baseMaterial)
		{
			GetComponents(typeof(IMaterialModifier), components);
			var currentMat = baseMaterial;
			for (var i = 0; i < components.Count; i++)
				currentMat = (components[i] as IMaterialModifier).GetModifiedMaterial(currentMat);
			components.Clear();
			return currentMat;
		}


		protected override void UpdateMaterial()
		{
			base.UpdateMaterial();
			if (shouldEmojilize)
			{
				if (IsActive())
				{
					CreateEmojiCanvasRenderer();

					emojiCanvasRenderer.materialCount = 1;
					if (config.material != null)
						emojiCanvasRenderer.SetMaterial(GetModifiedEmojiMaterial(config.material), 0);
					else
						emojiCanvasRenderer.SetMaterial(materialForRendering, 0);
					emojiCanvasRenderer.SetTexture(config.texture);
				}
			}
			else
			{
				if (emojiCanvasRenderer != null)
				{
					emojiCanvasRenderer.Clear();
				}
			}
		}

		protected virtual string RaycastOnHrefs(Vector2 sp, Camera eventCamera)
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
						var baseIndex = (hrefStartIndex + j) * 4;
						if (baseIndex <= hrefVh.Count - 4)
						{
							var xMin = hrefVh[baseIndex];
							var xMax = hrefVh[baseIndex + 1];
							var yMin = hrefVh[baseIndex + 2];
							var yMax = hrefVh[baseIndex + 3];
							if (local.x < xMin || local.x > xMax)
							{
								continue;
							}
							if (local.y < yMin || local.y > yMax)
							{
								continue;
							}
							return h.href; // once a time
						}
					}
					hrefStartIndex += count;
				}
			}
			return string.Empty;
		}

		public override bool Raycast(Vector2 sp, Camera eventCamera)
		{
			var ret = base.Raycast(sp, eventCamera);
			if (ret)
			{
				if (shouldEmojilize)
				{
					return hrefOnClickedEvent.GetPersistentEventCount() > 0
						&& !string.IsNullOrEmpty(RaycastOnHrefs(sp,	eventCamera));
				}
				else
				{
					return false;
				}
			}
			return ret;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			var href = RaycastOnHrefs(eventData.position, eventData.pressEventCamera);
			if (!string.IsNullOrEmpty(href))
			{ 
				hrefOnClickedEvent.Invoke(hrefOnClickedEventName, href);
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
		}

		public void OnPointerUp(PointerEventData eventData)
		{
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
		}

		public void OnPointerExit(PointerEventData eventData)
		{
		}
	}
}
