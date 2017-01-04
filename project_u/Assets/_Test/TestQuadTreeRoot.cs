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
using scene;
using System.Linq;
using System;


public class TestQuadTreeRoot : MonoBehaviour {

	class Obj : scene.IBounds
	{
		MeshRenderer mr;
		public Obj(MeshRenderer mr)
		{
			this.mr = mr;
		}
        public Bounds GetBounds()
		{
			return mr.bounds;
		}
	}

	GenericQuadTree<Obj> quadTree;

	public void AddAllSubObjectToQuadTree(int minSize, float loose)
	{
		var objs = gameObject.GetComponentsInChildren<Transform>()
			.Select(t => t.GetComponent<MeshRenderer>())
			.Where(mr => mr != null)
			.Select( mr => new Obj(mr));
		quadTree = GenericQuadTree<Obj>.BuildFrom(objs, minSize, loose);
	}

	void OnDrawGizmos()
	{
		if (quadTree != null)
		{

		}
	}

}
