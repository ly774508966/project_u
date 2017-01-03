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
using utils.primitive;

namespace utils
{
	public class MappingAxis
	{
		public enum Plane
		{
			XZ = 0,
			XY,
			YZ
		}

		public static Plane mappingPlane = Plane.XZ;

		static int[] mappingX = new int[] { 0, 0, 1 };
		static int[] mappingY = new int[] { 2, 1, 2 };

		public static Vector2 Map(Vector3 p)
		{
			return new Vector2(p[mappingX[(int)mappingPlane]], p[mappingY[(int)mappingPlane]]);
		}

		public static Vector3 Map(Vector2 p)
		{
			Vector3 ret = new Vector3(0f, 0f, 0f);
			ret[mappingX[(int)mappingPlane]] = p.x;
			ret[mappingY[(int)mappingPlane]] = p.y;
			return ret;
		}
		public static Line2d Map(Line L)
		{
			return new Line2d(Map(L.P), Map(L.D));
		}

		public static void SetPosition(ref Vector3 p0, Vector3 p1)
		{
			Vector2 pp1 = Map(p1);
			p0[mappingX[(int)mappingPlane]] = pp1.x;
			p0[mappingY[(int)mappingPlane]] = pp1.y;
		}
	}
}