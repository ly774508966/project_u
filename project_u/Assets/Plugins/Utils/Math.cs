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
using System.Collections;

namespace utils
{
	public class Math
	{
		const float kEpsilon = 0.001f;

		public static bool Approximately(float a, float b, float epsilon = kEpsilon)
		{
			return Mathf.Abs(a - b) < epsilon;
		}

		public static bool LessOrEqual(float a, float b)
		{
			return a < b || Approximately(a, b);
		}

		public static bool GreaterOrEqual(float a, float b)
		{
			return a > b || Approximately(a, b);
		}

		public static bool Approximately(Vector3 a, Vector3 b)
		{
			for (int i = 0; i < 3; ++i)
			{
				if (!Approximately(a[i], b[i]))
					return false;
			}
			return true;
		}




		public struct Line
		{
			public Vector3 P;
			public Vector3 D_;
			public Vector3 D
			{
				get
				{
					return D_;
				}
				set
				{
					D_ = value.normalized;
				}
			}

			public Line(Vector3 p, Vector3 d)
			{
				P = p;
				D_ = d.normalized;
			}
		}

		public static bool Intersects(Plane a, Plane b, out Line line)
		{
			Vector3 dir = Vector3.Cross(a.normal, b.normal);
			Vector3 p = Vector3.zero;
			if (Approximately(dir.sqrMagnitude, 0f))
			{
				line = new Line();
				return false;
			}
			dir = dir.normalized;
			float dot = Vector3.Dot(a.normal, b.normal);
			float kd = 1f - dot * dot;
			float k0 = (-a.distance + b.distance * dot) / kd;
			float k1 = (-b.distance + a.distance * dot) / kd;
			p = k0 * a.normal + k1 * b.normal;
			line = new Line(p, dir);
			return true;
		}

		public static bool Intersects(Plane plane, Vector3 p0, Vector3 p1, out float enter)
		{
			var d = p1 - p0;
			if (Approximately(Vector3.Dot(plane.normal, d.normalized), 0f))
			{
				if (Approximately(Vector3.Dot(p0 - plane.normal * plane.distance, plane.normal), 0f))
				{
					enter = Mathf.Infinity;
					return true;
				}
				enter = 0f;
				return false;
			}
			float m = plane.distance - Vector3.Dot(p0, plane.normal);
			float deno = Vector3.Dot(d, plane.normal);
			enter = m / deno;
			return GreaterOrEqual(enter, 0f) && LessOrEqual(enter, 1f);
		}



		public static int MinAxis(Vector3 value)
		{
			float minValue = float.PositiveInfinity;
			int minAxis = 0;
			for (int i = 0; i < 3; ++i)
			{
				if (value[i] < minValue)
				{
					minValue = value[i];
					minAxis = i;
				}
			}
			return minAxis;
		}

		public static int MaxAxis(Vector3 value)
		{
			float maxValue = float.NegativeInfinity;
			int minAxis = 0;
			for (int i = 0; i < 3; ++i)
			{
				if (value[i] > maxValue)
				{
					maxValue = value[i];
					minAxis = i;
				}
			}
			return minAxis;
		}

	}
}