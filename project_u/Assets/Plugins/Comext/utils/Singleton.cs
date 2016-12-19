using UnityEngine;
using System.Collections;
using System;

namespace comext.utils
{
	public class Singleton<T> where T : Singleton<T>
	{
		static object instancingLock = new object();

		static T instance_;
		public static T instance
		{
			get
			{
				if (instance_ != null) return instance_;
				lock (instancingLock)
				{
					if (instance_ == null)
					{
						instance_ = System.Activator.CreateInstance<T>();
					}
				}
				return instance_;
			}
		}

		public static void DestroyInstance()
		{
			App.AssertMainThread();
			lock (instancingLock)
			{
				if (instance_ != null) instance_.OnSingletonDestroy();
				instance_ = null;
			}
		}

		protected Singleton() { }

		protected virtual void OnSingletonDestroy()
		{
		}
		
	}

}
